using System.Collections.Generic;

namespace OccultShop.Models;

public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? IconPath { get; set; }
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public int BasePrice { get; set; }
}
