using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class OnlineRoomStageController : MonoBehaviour
    {
        private void Start()
        {
            ApplyRoomStage();
        }

        private void ApplyRoomStage()
        {
            DoudizhuUiController gameController = GetComponent<DoudizhuUiController>();
            if (gameController != null)
            {
                gameController.enabled = false;
            }

            SetText("TopBar/Status", $"联机房间 | 桌子 {OnlineRoomSession.TableId}");
            SetText("TableArea/CenterTip", "等待玩家准备");

            SetNodeActive("ActionBar", false);
            SetNodeActive("TableArea/RestartButton", false);
            SetNodeActive("BottomCards", false);
            SetNodeActive("HandArea", false);

            List<string> players = new List<string>(OnlineRoomSession.Players);
            string localName = OnlineRoomSession.LocalPlayerName;
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = "我";
            }

            SetPanel("PlayerPanel_Bottom", localName, true);

            List<string> others = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != localName)
                {
                    others.Add(players[i]);
                }
            }

            SetPanel("PlayerPanel_Left", others.Count > 0 ? others[0] : "空位", others.Count > 0);
            SetPanel("PlayerPanel_Right", others.Count > 1 ? others[1] : "空位", others.Count > 1);
        }

        private void SetPanel(string panelPath, string playerName, bool occupied)
        {
            SetText($"{panelPath}/NameText", playerName);
            SetText($"{panelPath}/CoinText", occupied ? "在线" : "--");
            SetText($"{panelPath}/RoleText", occupied ? "准备" : "空");
            SetText($"{panelPath}/CardCountText", string.Empty);
        }

        private void SetText(string path, string text)
        {
            Text target = transform.Find(path)?.GetComponent<Text>();
            if (target != null)
            {
                target.text = text;
            }
        }

        private void SetNodeActive(string path, bool active)
        {
            Transform node = transform.Find(path);
            if (node != null)
            {
                node.gameObject.SetActive(active);
            }
        }
    }
}
