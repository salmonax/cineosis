using UnityEngine.Video;
using UnityEngine;

/* ClipPool is manages the caching and preparing of clips
 * from a limited number of VideoPlayers.
 */
public class ClipPool
{
    VideoPlayer[] _clipPlayers;
    Texture2D[] _matteTextures;
    int _curClipCursor = 0;

    public int nextIndex(int increment = 1)
    {
        Debug.Log("!!! " + increment);
        var summedIndex = _curClipCursor + increment;
        if (summedIndex < 0)
            summedIndex = (summedIndex % _clipPlayers.Length) + _clipPlayers.Length;
        return summedIndex % _clipPlayers.Length;
    }
    void moveCursor(int offset) => _curClipCursor = nextIndex(offset);

    public VideoPlayer[] clips
    {
        get => _clipPlayers;
    }
    public VideoPlayer current
    {
        get =>_clipPlayers[_curClipCursor];
    }
    public Texture2D currentMatte
    {
        get => _matteTextures[_curClipCursor];
    }
    public int index
    {
        get => _curClipCursor;
    }

    public void Next(System.Action<VideoPlayer, int> onChange, int offset = 1)
    {
        moveCursor(offset);
        int sign = (int) Mathf.Sign(offset);
        if (current.isPrepared)
        {
            Debug.Log("ClipPool Already Prepared on Next()");
            _clipPlayers[nextIndex(sign * 1)].Prepare();
            _clipPlayers[nextIndex(sign * 2)].Prepare();
            onChange(current, index);
        }
        else
        {
            current.prepareCompleted += (prepared) =>
            {
                Debug.Log("ClipPool called after prepare: " + offset);

                _clipPlayers[nextIndex(sign * 1)].Prepare();
                _clipPlayers[nextIndex(sign * 2)].Prepare();
                onChange(prepared, index);
            };
            current.Prepare();
        }

        //current.prepareCompleted += 



        // Do more business here
    }

    public ClipPool(string[] clipList)
    {
        _clipPlayers = new VideoPlayer[clipList.Length];
        _matteTextures = new Texture2D[clipList.Length];
        for (int i = 0; i < clipList.Length; i++)
        {
            _clipPlayers[i] = ClipProvider.GetExternal(clipList[i]);
            _matteTextures[i] = ClipProvider.GetExternalMatte(clipList[i]);
        }
        //_clipPlayers[0].Prepare();
        //_clipPlayers[nextIndex(1)].Prepare(); // won't fail on 1 video
    }
}