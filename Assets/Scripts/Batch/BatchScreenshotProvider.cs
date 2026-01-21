using UnityEngine;

namespace BatchTools
{
    public class BatchScreenshotProvider : MonoBehaviour, IBatchScreenshotProvider
    {
        public void CaptureScreenshots(IBatchScreenshotContext context)
        {
            if (context == null)
            {
                return;
            }

            context.Capture(string.Empty);
        }
    }
}
