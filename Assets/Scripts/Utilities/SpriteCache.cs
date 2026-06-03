using UnityEngine;

public static class SpriteCache
{
    private static Sprite[] _communitySprites;
    private static Sprite[] _baseSprites;

    public static Sprite[] GetCommunitySprites()
    {
        // Load only if it hasn't been loaded yet
        if (_communitySprites == null)
        {
            _communitySprites = Resources.LoadAll<Sprite>("community_atlas_image");
        }
        return _communitySprites;
    }

    public static Sprite[] GetBaseSprites()
    {
        // Load only if it hasn't been loaded yet
        if (_baseSprites == null)
        {
            _baseSprites = Resources.LoadAll<Sprite>("base_atlas_image");
        }
        return _baseSprites;
    }
}