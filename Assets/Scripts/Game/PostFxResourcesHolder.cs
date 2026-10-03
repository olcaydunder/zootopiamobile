using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Points at the Post Processing package's shader resources so they ship in the build and
/// the post-processing layer can be created from code (Resources/PostFxResources.asset).
/// </summary>
public class PostFxResourcesHolder : ScriptableObject
{
    public PostProcessResources resources;
}
