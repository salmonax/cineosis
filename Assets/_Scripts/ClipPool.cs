using UnityEngine.Video;

/* ClipPool is manages the caching and preparing of clips
 * from a limited number of VideoPlayers.
 */
public class ClipPool
{
    VideoPlayer[] _clipPlayers;
    int _curClipCursor = 0;

    public int nextIndex(int increment = 1)
    {
        return (_curClipCursor + increment) % _clipPlayers.Length;
    }
    void incrementCursor() => _curClipCursor = nextIndex();

    public VideoPlayer[] clips
    {
        get => _clipPlayers;
    }
    public VideoPlayer current
    {
        get
        {
            //            Debug.Log("CALLED!");
            return _clipPlayers[_curClipCursor];
        }
    }
    public int index
    {
        get => _curClipCursor;
    }

    public void Next(System.Action<VideoPlayer, int> onChange)
    {
        incrementCursor();
        if (current.isPrepared)
        {
            _clipPlayers[nextIndex(1)].Prepare();
            _clipPlayers[nextIndex(2)].Prepare();
            onChange(current, index);
        }
        else
        {
            current.prepareCompleted += (_) =>
            {
                _clipPlayers[nextIndex(1)].Prepare();
                _clipPlayers[nextIndex(2)].Prepare();
                onChange(current, index);
            };
            current.Prepare();
        }

        //current.prepareCompleted += 



        // Do more business here
    }

    public ClipPool(string[] clipList)
    {
        _clipPlayers = new VideoPlayer[clipList.Length];
        for (int i = 0; i < clipList.Length; i++)
        {
            _clipPlayers[i] = ClipProvider.GetExternal(clipList[i]);
        }
        _clipPlayers[0].Prepare();
        _clipPlayers[nextIndex(1)].Prepare(); // won't fail on 1 video
    }
}