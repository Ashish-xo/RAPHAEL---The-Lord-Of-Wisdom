using Raphael.UI.ModContent.Data;
using UnityEngine;

namespace Raphael.UI.Framework.UniverseLib.UI.Panels;

public interface IPanelBase
{
    PanelType PanelType { get; }
    string PanelId { get; }
    bool Enabled { get; set; }
    RectTransform Rect { get; }
    GameObject UIRoot { get; }
    PanelDragger Dragger { get; }

    void Destroy();
    void EnsureValidSize();
    void EnsureValidPosition();
    void SetActive(bool active);
    void SetActiveOnly(bool active);
}