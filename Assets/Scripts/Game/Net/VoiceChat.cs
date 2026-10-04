using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Voice chat with friends and teammates (open microphone, sent only while you speak).
/// The microphone (16 kHz) is reduced to 8 kHz, cut into 40 ms frames and packed with IMA ADPCM
/// (4 bits per sample, 160 bytes per frame, ~32 kbit/s); each frame carries its own decoder start
/// state, so a lost packet costs only its 40 ms. The game server forwards frames to the room
/// (private room waiting room) or to living teammates (match). Blocked or muted players are not played.
/// </summary>
public class VoiceChat : MonoBehaviour
{
    public const int NetRate = 8000;
    public const int FrameSamples = 320;              // 40 ms
    private const int MicRate = 16000;
    private const float HangoverSeconds = 0.35f;      // keep sending a moment after you stop talking

    public static VoiceChat Instance { get; private set; }

    /// <summary>Microphone on (open mic while in a voice room).</summary>
    public static bool MicOn
    {
        get { return PlayerPrefs.GetInt("zm_mic", 1) == 1; }
        set { PlayerPrefs.SetInt("zm_mic", value ? 1 : 0); }
    }

    /// <summary>Hear the others.</summary>
    public static bool SpeakerOn
    {
        get { return PlayerPrefs.GetInt("zm_voice_out", 1) == 1; }
        set { PlayerPrefs.SetInt("zm_voice_out", value ? 1 : 0); }
    }

    /// <summary>Players muted for this session (account ids), besides the blocked ones.</summary>
    public static readonly HashSet<string> Muted = new HashSet<string>();

    /// <summary>True while our own voice is being sent.</summary>
    public static bool Speaking { get; private set; }
    /// <summary>Microphone could not be used (no permission / no device): shown on the button.</summary>
    public static string MicProblem { get; private set; }

    private AudioClip micClip;
    private int micPos;
    private int micRate;
    private float[] micRead = new float[4096];
    private readonly List<float> pending = new List<float>(4096);
    private float resampleAcc;
    private int resampleCount;
    private float noiseFloor = 0.004f;
    private float lastVoiceTime = -10f;
    private byte seq;
    private readonly byte[] packet = new byte[4 + FrameSamples / 2];
    private readonly short[] pcm = new short[FrameSamples];
    private bool permissionAsked;

    private class Speaker
    {
        public VoiceOut output;
        public string name = "";
        public float lastHeard = -10f;
    }

    private readonly Dictionary<int, Speaker> speakers = new Dictionary<int, Speaker>();
    private static readonly short[] decodeBuffer = new short[FrameSamples];

    public static VoiceChat Ensure()
    {
        if (Instance == null && !NetGame.IsServer)
        {
            var go = new GameObject("VoiceChat");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VoiceChat>();
        }
        return Instance;
    }

    /// <summary>Name of whoever is talking right now (for the HUD), or "".</summary>
    public static string TalkingNow()
    {
        if (Instance == null)
            return "";
        string names = "";
        foreach (var s in Instance.speakers.Values)
        {
            if (Time.unscaledTime - s.lastHeard < 0.4f && s.name.Length > 0)
                names = names.Length == 0 ? s.name : names + ", " + s.name;
        }
        return names;
    }

    private void Update()
    {
        var net = NetClient.Instance;
        bool want = MicOn && net != null && net.VoiceAvailable && Application.isFocused;
        if (want && micClip == null)
            StartMic();
        else if (!want && micClip != null)
            StopMic();
        if (micClip != null)
            Capture(net);
        else
            Speaking = false;

        // Forget voices that have gone quiet for a while.
        if (speakers.Count > 0 && (net == null || (net.State != NetClient.Phase.Lobby && net.State != NetClient.Phase.Playing)))
            ClearSpeakers();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            StopMic();
    }

    // ----- Microphone -----

    private void StartMic()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (!permissionAsked)
            {
                permissionAsked = true;
                Permission.RequestUserPermission(Permission.Microphone);
            }
            MicProblem = "Mikrofon izni yok";
            return;
        }
#endif
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            MicProblem = "Mikrofon bulunamadı";
            return;
        }
        MicProblem = null;
        micClip = Microphone.Start(null, true, 1, MicRate);
        micRate = micClip != null ? micClip.frequency : MicRate;
        micPos = 0;
        pending.Clear();
        resampleAcc = 0f;
        resampleCount = 0;
    }

    private void StopMic()
    {
        if (micClip != null)
        {
            Microphone.End(null);
            Destroy(micClip);
        }
        micClip = null;
        Speaking = false;
    }

    private void Capture(NetClient net)
    {
        int pos = Microphone.GetPosition(null);
        int length = micClip.samples;
        if (pos < 0 || length <= 0)
            return;
        int available = (pos - micPos + length) % length;
        if (available <= 0)
            return;
        if (available > micRead.Length)
            micRead = new float[available];
        // Read in up to two pieces (the clip loops).
        int first = Mathf.Min(available, length - micPos);
        ReadMic(micPos, first, 0);
        if (available > first)
            ReadMic(0, available - first, first);
        micPos = pos;

        // 16 kHz (or whatever the device gives) -> 8 kHz by averaging.
        float ratio = micRate / (float)NetRate;
        for (int i = 0; i < available; i++)
        {
            resampleAcc += micRead[i];
            resampleCount++;
            if (resampleCount >= ratio)
            {
                pending.Add(resampleAcc / resampleCount);
                resampleAcc = 0f;
                resampleCount = 0;
            }
        }

        while (pending.Count >= FrameSamples)
        {
            SendFrame(net);
            pending.RemoveRange(0, FrameSamples);
        }
    }

    private void ReadMic(int offset, int count, int into)
    {
        if (count <= 0)
            return;
        var tmp = new float[count];
        micClip.GetData(tmp, offset);
        System.Array.Copy(tmp, 0, micRead, into, count);
    }

    private void SendFrame(NetClient net)
    {
        // Loudness of this frame against a slowly adapting noise floor (open mic: only speech goes out).
        float sum = 0f;
        for (int i = 0; i < FrameSamples; i++)
            sum += pending[i] * pending[i];
        float rms = Mathf.Sqrt(sum / FrameSamples);
        bool loud = rms > Mathf.Max(0.012f, noiseFloor * 3f);
        noiseFloor = loud ? Mathf.Lerp(noiseFloor, rms, 0.002f) : Mathf.Lerp(noiseFloor, rms, 0.05f);
        if (loud)
            lastVoiceTime = Time.unscaledTime;
        Speaking = Time.unscaledTime - lastVoiceTime < HangoverSeconds;
        if (!Speaking)
            return;

        for (int i = 0; i < FrameSamples; i++)
            pcm[i] = (short)Mathf.Clamp(pending[i] * 32767f * 1.5f, -32768f, 32767f);
        int predictor = pcm[0];
        int index = AdpcmStartIndex;
        packet[0] = seq++;
        packet[1] = (byte)predictor;
        packet[2] = (byte)(predictor >> 8);
        packet[3] = (byte)index;
        Adpcm.Encode(pcm, FrameSamples, ref predictor, ref index, packet, 4);
        net.SendVoice(packet, packet.Length);
    }

    private const int AdpcmStartIndex = 20;

    // ----- Playback -----

    /// <summary>A voice frame from the server (payload = everything after the speaker id).</summary>
    public static void Receive(int speakerId, string account, byte[] data, int offset, int length)
    {
        if (!SpeakerOn || length != 4 + FrameSamples / 2)
            return;
        if (!string.IsNullOrEmpty(account) && (Muted.Contains(account) || IsBlocked(account)))
            return;
        var vc = Ensure();
        if (vc == null)
            return;
        Speaker s;
        if (!vc.speakers.TryGetValue(speakerId, out s))
        {
            var go = new GameObject("Voice_" + speakerId);
            go.transform.SetParent(vc.transform, false);
            s = new Speaker { output = go.AddComponent<VoiceOut>() };
            vc.speakers[speakerId] = s;
        }
        var net = NetClient.Instance;
        s.name = net != null ? net.NameOfPlayer(speakerId) : "";
        s.lastHeard = Time.unscaledTime;
        int predictor = (short)(data[offset + 1] | (data[offset + 2] << 8));
        int index = Mathf.Clamp(data[offset + 3], 0, 88);
        Adpcm.Decode(data, offset + 4, FrameSamples, ref predictor, ref index, decodeBuffer);
        s.output.Push(decodeBuffer, FrameSamples);
    }

    public static bool IsBlocked(string account)
    {
        var social = OnlineService.Social;
        if (social == null || social.blocked == null)
            return false;
        foreach (var b in social.blocked)
            if (b != null && b.id == account)
                return true;
        return false;
    }

    private void ClearSpeakers()
    {
        foreach (var s in speakers.Values)
            if (s.output != null)
                Destroy(s.output.gameObject);
        speakers.Clear();
    }
}

/// <summary>Plays one speaker's voice: a small jitter buffer read on the audio thread at the output rate.</summary>
public class VoiceOut : MonoBehaviour
{
    private const int Prebuffer = VoiceChat.NetRate * 12 / 100;   // 120 ms before starting
    private const int MaxBuffered = VoiceChat.NetRate;            // drop the oldest beyond 1 s

    private readonly Queue<float> queue = new Queue<float>(VoiceChat.NetRate);
    private readonly object gate = new object();
    private bool playing;
    private double readPos;
    private float last, next;
    private double step;

    private void Awake()
    {
        step = VoiceChat.NetRate / (double)Mathf.Max(8000, AudioSettings.outputSampleRate);
        var src = gameObject.AddComponent<AudioSource>();
        // OnAudioFilterRead needs a playing source: a looping clip of ones that the filter overwrites.
        var clip = AudioClip.Create("VoiceCarrier", 1024, 1, AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000, false);
        var ones = new float[1024];
        for (int i = 0; i < ones.Length; i++)
            ones[i] = 1f;
        clip.SetData(ones, 0);
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;
        src.bypassEffects = true;
        src.priority = 0;
        src.volume = 1f;
        src.Play();
    }

    public void Push(short[] samples, int count)
    {
        lock (gate)
        {
            for (int i = 0; i < count; i++)
                queue.Enqueue(samples[i] / 32768f);
            while (queue.Count > MaxBuffered)
                queue.Dequeue();
            if (!playing && queue.Count >= Prebuffer)
            {
                playing = true;
                readPos = 0;
                last = queue.Dequeue();
                next = queue.Count > 0 ? queue.Dequeue() : last;
            }
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (gate)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                float v = 0f;
                if (playing)
                {
                    v = last + (next - last) * (float)readPos;
                    readPos += step;
                    while (readPos >= 1.0)
                    {
                        readPos -= 1.0;
                        last = next;
                        if (queue.Count > 0)
                            next = queue.Dequeue();
                        else
                        {
                            playing = false;   // ran dry: wait for the buffer to refill
                            break;
                        }
                    }
                    v = Mathf.Clamp(v * 1.6f, -1f, 1f);
                }
                for (int c = 0; c < channels; c++)
                    data[i + c] = v;
            }
        }
    }
}

/// <summary>IMA ADPCM, 4 bits per sample (two samples per byte, low nibble first).</summary>
public static class Adpcm
{
    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
    private static readonly int[] StepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060,
        1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
        7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };

    public static void Encode(short[] pcm, int count, ref int predictor, ref int index, byte[] output, int offset)
    {
        for (int i = 0; i < count; i++)
        {
            int step = StepTable[index];
            int diff = pcm[i] - predictor;
            int code = 0;
            if (diff < 0)
            {
                code = 8;
                diff = -diff;
            }
            int delta = step >> 3;
            if (diff >= step) { code |= 4; diff -= step; delta += step; }
            step >>= 1;
            if (diff >= step) { code |= 2; diff -= step; delta += step; }
            step >>= 1;
            if (diff >= step) { code |= 1; delta += step; }
            predictor += (code & 8) != 0 ? -delta : delta;
            predictor = predictor < -32768 ? -32768 : predictor > 32767 ? 32767 : predictor;
            index += IndexTable[code];
            index = index < 0 ? 0 : index > 88 ? 88 : index;
            int b = offset + (i >> 1);
            if ((i & 1) == 0)
                output[b] = (byte)code;
            else
                output[b] |= (byte)(code << 4);
        }
    }

    public static void Decode(byte[] input, int offset, int count, ref int predictor, ref int index, short[] pcm)
    {
        for (int i = 0; i < count; i++)
        {
            int b = input[offset + (i >> 1)];
            int code = (i & 1) == 0 ? b & 15 : b >> 4;
            int step = StepTable[index];
            int delta = step >> 3;
            if ((code & 4) != 0) delta += step;
            if ((code & 2) != 0) delta += step >> 1;
            if ((code & 1) != 0) delta += step >> 2;
            predictor += (code & 8) != 0 ? -delta : delta;
            predictor = predictor < -32768 ? -32768 : predictor > 32767 ? 32767 : predictor;
            index += IndexTable[code];
            index = index < 0 ? 0 : index > 88 ? 88 : index;
            pcm[i] = (short)predictor;
        }
    }
}
