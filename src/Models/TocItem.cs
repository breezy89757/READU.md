// READU.md — Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ReadU.Models;

public class TocItem
{
    public string Title { get; set; }
    public int Level { get; set; }
    public string Id { get; set; }

    public Thickness IndentMargin => new((Level - 1) * 12, 4, 0, 4);
}
