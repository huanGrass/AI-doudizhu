namespace BatchTools
{
    public interface IBatchScreenshotProvider
    {
        void CaptureScreenshots(IBatchScreenshotContext context);
    }

    public interface IBatchScreenshotContext
    {
        void Capture(string tag);
    }
}
