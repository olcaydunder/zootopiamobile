using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

/// <summary>
/// Checks that a Google Play purchase really comes from Google Play: Play signs every purchase (the purchase JSON) with
/// the app's own key, and the matching public key (Play Console > Monetisation setup > Licensing) is in PlayConfig.
/// A purchase made up by a "free purchase" tool has no valid signature and gives no Kredi.
///
/// The receipt is Unity IAP's unified receipt: {"Store":"GooglePlay","TransactionID":..,"Payload":"{\"json\":..,
/// \"signature\":..}"}. Plain C# (SHA-1 + RSA PKCS#1 v1.5 with BigInteger, no platform crypto) so it behaves the
/// same on every phone and can be tested off the device.
/// </summary>
public static class ReceiptCheck
{
    public enum Result
    {
        /// <summary>Signed by Google Play for this game and this product.</summary>
        Valid,
        /// <summary>Has a Google Play signature but it is wrong, or it is for another game / product: give nothing.</summary>
        Invalid,
        /// <summary>Not a Google Play receipt this code knows (no signed purchase in it).</summary>
        Unknown,
    }

    /// <summary>
    /// Checks the receipt. <paramref name="productId"/> is the product being given ("" = don't check the product).
    /// <paramref name="why"/> says what was wrong (for the log).
    /// </summary>
    public static Result Check(string receipt, string publicKeyBase64, string packageName, string productId, out string why)
    {
        why = "";
        if (string.IsNullOrEmpty(receipt))
        {
            why = "empty receipt";
            return Result.Unknown;
        }
        var top = Json.Object(receipt);
        if (top == null)
        {
            why = "receipt is not JSON";
            return Result.Unknown;
        }
        string store = Json.Get(top, "Store");
        if (store.Length > 0 && store != "GooglePlay")
        {
            why = "store " + store;
            return Result.Unknown;
        }
        var payload = Json.Object(Json.Get(top, "Payload"));
        if (payload == null || !payload.ContainsKey("json"))
        {
            why = "no signed purchase in the receipt";
            return Result.Unknown;
        }
        string purchaseJson = Json.Get(payload, "json");
        string signature = Json.Get(payload, "signature");
        if (purchaseJson.Length == 0 || signature.Length == 0)
        {
            why = "purchase or signature is empty";
            return Result.Invalid;
        }

        byte[] modulus, exponent;
        if (!ReadPublicKey(publicKeyBase64, out modulus, out exponent))
        {
            why = "public key could not be read";
            return Result.Unknown;   // a broken key in the build must not take everybody's Kredi
        }
        byte[] sig;
        try
        {
            sig = Convert.FromBase64String(signature.Trim());
        }
        catch (FormatException)
        {
            why = "signature is not base64";
            return Result.Invalid;
        }
        if (!VerifySha1Rsa(Encoding.UTF8.GetBytes(purchaseJson), sig, modulus, exponent))
        {
            why = "signature does not match";
            return Result.Invalid;
        }

        var purchase = Json.Object(purchaseJson);
        if (purchase == null)
        {
            why = "signed purchase is not JSON";
            return Result.Invalid;
        }
        string pkg = Json.Get(purchase, "packageName");
        if (!string.IsNullOrEmpty(packageName) && pkg != packageName)
        {
            why = "purchase is for " + pkg;
            return Result.Invalid;
        }
        if (!string.IsNullOrEmpty(productId))
        {
            string one = Json.Get(purchase, "productId");
            string many = purchase.ContainsKey("productIds") ? purchase["productIds"] : "";
            bool listed = one == productId || many.Contains("\"" + productId + "\"");
            if ((one.Length > 0 || many.Length > 0) && !listed)
            {
                why = "purchase is for " + one + many;
                return Result.Invalid;
            }
        }
        string state = Json.Get(purchase, "purchaseState");
        if (state.Length > 0 && state != "0")
        {
            why = "purchase state " + state;
            return Result.Invalid;
        }
        return Result.Valid;
    }

    // ----- RSA (PKCS#1 v1.5, SHA-1) -----

    // DER prefix of DigestInfo for SHA-1 (RFC 8017 9.2).
    private static readonly byte[] Sha1DigestInfo = { 0x30, 0x21, 0x30, 0x09, 0x06, 0x05, 0x2b, 0x0e, 0x03, 0x02, 0x1a, 0x05, 0x00, 0x04, 0x14 };

    public static bool VerifySha1Rsa(byte[] data, byte[] signature, byte[] modulus, byte[] exponent)
    {
        int k = modulus.Length;
        while (k > 0 && modulus[modulus.Length - k] == 0)
            k--;   // modulus length without leading zeros
        if (k < 64 || signature.Length > k + 1)
            return false;
        var n = Unsigned(modulus);
        var s = Unsigned(signature);
        if (s >= n)
            return false;
        byte[] em = BigEndian(BigInteger.ModPow(s, Unsigned(exponent), n), k);

        byte[] hash;
        using (var sha = new System.Security.Cryptography.SHA1Managed())
            hash = sha.ComputeHash(data);
        int tLen = Sha1DigestInfo.Length + hash.Length;
        if (k < tLen + 11)
            return false;
        // 00 01 FF..FF 00 DigestInfo hash
        int diff = em[0] | (em[1] ^ 0x01);
        int psEnd = k - tLen - 1;
        for (int i = 2; i < psEnd; i++)
            diff |= em[i] ^ 0xFF;
        diff |= em[psEnd];
        for (int i = 0; i < Sha1DigestInfo.Length; i++)
            diff |= em[psEnd + 1 + i] ^ Sha1DigestInfo[i];
        for (int i = 0; i < hash.Length; i++)
            diff |= em[k - hash.Length + i] ^ hash[i];
        return diff == 0;
    }

    private static BigInteger Unsigned(byte[] bigEndian)
    {
        var le = new byte[bigEndian.Length + 1];   // extra 0 byte keeps it positive
        for (int i = 0; i < bigEndian.Length; i++)
            le[i] = bigEndian[bigEndian.Length - 1 - i];
        return new BigInteger(le);
    }

    private static byte[] BigEndian(BigInteger v, int length)
    {
        byte[] le = v.ToByteArray();
        var be = new byte[length];
        for (int i = 0; i < le.Length && i < length; i++)
            be[length - 1 - i] = le[i];
        return be;
    }

    /// <summary>Reads an RSA public key in X.509 SubjectPublicKeyInfo form (base64 DER), the form Play Console gives.</summary>
    public static bool ReadPublicKey(string base64, out byte[] modulus, out byte[] exponent)
    {
        modulus = exponent = null;
        try
        {
            byte[] der = Convert.FromBase64String((base64 ?? "").Trim());
            int p = 0;
            int end;
            if (!Enter(der, ref p, 0x30, out end))       // SubjectPublicKeyInfo
                return false;
            int algEnd;
            if (!Enter(der, ref p, 0x30, out algEnd))    // AlgorithmIdentifier
                return false;
            int oidEnd;
            if (!Enter(der, ref p, 0x06, out oidEnd))
                return false;
            byte[] rsaOid = { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
            if (oidEnd - p != rsaOid.Length)
                return false;
            for (int i = 0; i < rsaOid.Length; i++)
                if (der[p + i] != rsaOid[i])
                    return false;
            p = algEnd;
            int bitsEnd;
            if (!Enter(der, ref p, 0x03, out bitsEnd) || der[p] != 0)   // BIT STRING, no unused bits
                return false;
            p++;
            int keyEnd;
            if (!Enter(der, ref p, 0x30, out keyEnd))    // RSAPublicKey
                return false;
            int nEnd;
            if (!Enter(der, ref p, 0x02, out nEnd))
                return false;
            modulus = Slice(der, p, nEnd);
            p = nEnd;
            int eEnd;
            if (!Enter(der, ref p, 0x02, out eEnd))
                return false;
            exponent = Slice(der, p, eEnd);
            return modulus.Length >= 64 && exponent.Length > 0;
        }
        catch (Exception)
        {
            modulus = exponent = null;
            return false;
        }
    }

    // Reads a DER tag + length; p moves to the content, end is where the content ends.
    private static bool Enter(byte[] der, ref int p, byte tag, out int end)
    {
        end = 0;
        if (p >= der.Length || der[p] != tag)
            return false;
        p++;
        int len = der[p++];
        if ((len & 0x80) != 0)
        {
            int bytes = len & 0x7f;
            if (bytes < 1 || bytes > 3)
                return false;
            len = 0;
            for (int i = 0; i < bytes; i++)
                len = (len << 8) | der[p++];
        }
        end = p + len;
        return end <= der.Length;
    }

    private static byte[] Slice(byte[] a, int from, int to)
    {
        var r = new byte[to - from];
        Array.Copy(a, from, r, 0, r.Length);
        return r;
    }

    // ----- tiny JSON reader (one level: string/number/bool values; objects and arrays kept as raw text) -----

    private static class Json
    {
        public static string Get(Dictionary<string, string> o, string key)
        {
            string v;
            return o != null && o.TryGetValue(key, out v) && v != null ? v : "";
        }

        public static Dictionary<string, string> Object(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            int p = 0;
            Space(text, ref p);
            if (p >= text.Length || text[p] != '{')
                return null;
            p++;
            var o = new Dictionary<string, string>();
            Space(text, ref p);
            if (p < text.Length && text[p] == '}')
                return o;
            while (p < text.Length)
            {
                Space(text, ref p);
                string key;
                if (!String(text, ref p, out key))
                    return null;
                Space(text, ref p);
                if (p >= text.Length || text[p] != ':')
                    return null;
                p++;
                Space(text, ref p);
                string value;
                if (!Value(text, ref p, out value))
                    return null;
                o[key] = value;
                Space(text, ref p);
                if (p >= text.Length)
                    return null;
                if (text[p] == ',')
                {
                    p++;
                    continue;
                }
                return text[p] == '}' ? o : null;
            }
            return null;
        }

        private static void Space(string t, ref int p)
        {
            while (p < t.Length && char.IsWhiteSpace(t[p]))
                p++;
        }

        private static bool Value(string t, ref int p, out string value)
        {
            value = null;
            if (p >= t.Length)
                return false;
            char c = t[p];
            if (c == '"')
                return String(t, ref p, out value);
            if (c == '{' || c == '[')
            {
                int start = p;
                if (!Skip(t, ref p))
                    return false;
                value = t.Substring(start, p - start);
                return true;
            }
            int s = p;
            while (p < t.Length && t[p] != ',' && t[p] != '}' && t[p] != ']' && !char.IsWhiteSpace(t[p]))
                p++;
            value = t.Substring(s, p - s);
            return value.Length > 0;
        }

        // Skips a whole object or array (strings inside may hold brackets).
        private static bool Skip(string t, ref int p)
        {
            int depth = 0;
            while (p < t.Length)
            {
                char c = t[p];
                if (c == '"')
                {
                    string unused;
                    if (!String(t, ref p, out unused))
                        return false;
                    continue;
                }
                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        p++;
                        return true;
                    }
                }
                p++;
            }
            return false;
        }

        private static bool String(string t, ref int p, out string value)
        {
            value = null;
            if (p >= t.Length || t[p] != '"')
                return false;
            p++;
            var sb = new StringBuilder();
            while (p < t.Length)
            {
                char c = t[p++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (p >= t.Length)
                    return false;
                char e = t[p++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (p + 4 > t.Length)
                            return false;
                        sb.Append((char)Convert.ToInt32(t.Substring(p, 4), 16));
                        p += 4;
                        break;
                    default:
                        return false;
                }
            }
            return false;
        }
    }
}
