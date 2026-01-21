# 批处理截图组件指引
本指引用于新项目创建 UI 批处理截图组件，供 `UIBatchRunner` 在批处理模式下调用。

## 1. 创建组件脚本
实现接口 `BatchTools.IBatchScreenshotProvider`，并挂到场景中可被扫描的对象上。

示例：
```csharp
using UnityEngine;
using BatchTools;

public class BatchScreenshotProvider : MonoBehaviour, IBatchScreenshotProvider
{
    public void CaptureScreenshots(IBatchScreenshotContext context)
    {
        if (context == null)
        {
            return;
        }

        // TODO: 切换到需要的状态
        context.Capture(string.Empty);
    }
}
```

说明：
- `context.Capture(tag)` 会生成 `Screenshots/ui_shot_*.png`，`tag` 用于区分状态。
- 若截图少于 1 张，`UIBatchRunner` 会补足默认截图。

## 2. 放入截图场景
将脚本挂到截图场景中的任意 GameObject 上。
`UIBatchRunner` 会扫描场景中的所有 `MonoBehaviour` 并调用实现了接口的组件。

## 3. 验证
运行批处理截图命令或一键检查：

```bat
ci/capture_screenshots.bat
```

截图输出目录：
- `Screenshots/ui_shot_1.png`

## 4. 常见问题
- 场景里找不到组件：确认脚本已挂载到场景对象并随场景保存。
- 只生成默认截图：说明没有扫描到任何 `IBatchScreenshotProvider` 实现。
