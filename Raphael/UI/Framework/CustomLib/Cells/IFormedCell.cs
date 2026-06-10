using System;
using Raphael.UI.Framework.UniverseLib.UI.Widgets.ScrollView;

namespace Raphael.UI.Framework.CustomLib.Cells;

public interface IFormedCell : ICell
{
    public int CurrentDataIndex { get; set; }
    public Action<int> OnClick { get; set; }
}