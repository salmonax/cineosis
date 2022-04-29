public partial class ClipManager
{
    public float _isDifferenceMaskEnabled
    {
        get => maskManager._isDifferenceMaskEnabled;
    }
    public void PullAndSetMaskState() => maskManager.PullAndSetMaskState();
    public void RenderColorMaskTick() => maskManager.RenderColorMaskTick();
    public void _resetFrameCapture(bool diffMaskOnly = true, bool clearDsts = true) =>
        maskManager._resetFrameCapture(diffMaskOnly, clearDsts);
}