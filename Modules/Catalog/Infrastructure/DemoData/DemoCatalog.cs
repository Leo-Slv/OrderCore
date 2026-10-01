namespace OrderCore.Api.Modules.Catalog.Infrastructure.DemoData;

public sealed record DemoProduct(
    string Sku,
    string Name,
    string Category,
    decimal Price,
    decimal? CompareAtPrice,
    string Maker,
    string ShortDescription,
    string Description,
    int Stock,
    int ReorderLevel = 0);

/// <summary>
/// The Marfim storefront's catalog, as drawn in its first two screens
/// (home and cart): four categories, eight pieces from four ateliers. The
/// atelier is the product's brand. Stock is varied on purpose so the store
/// shows every availability state: most pieces in stock, the Cadeira Lina
/// down to its last units (low stock), the Jarra Seixo sold out.
/// </summary>
public static class DemoCatalog
{
    public const string Currency = "BRL";

    public static readonly IReadOnlyList<string> Categories = ["Casa", "Cozinha", "Iluminação", "Têxteis"];

    private const string BarroCru = "Estúdio Barro Cru";
    private const string Tora = "Oficina Tora";
    private const string Faisca = "Oficina Faísca";
    private const string TearAlto = "Tear Alto";

    public static readonly IReadOnlyList<DemoProduct> Products =
    [
        new("MF-ARCO", "Luminária Arco", "Iluminação", 489m, null, Faisca,
            "Latão escovado · cúpula índigo",
            "Feita pela Oficina Faísca, em São Paulo (SP). Cúpula de vidro soprado à boca e haste de latão escovado.",
            Stock: 20),
        new("MF-ORBE", "Pendente Orbe", "Iluminação", 359m, 449m, Faisca,
            "Vidro soprado · latão escovado",
            "Feito pela Oficina Faísca, em São Paulo (SP). Globo de vidro soprado à boca com acabamentos de latão escovado.",
            Stock: 20),
        new("MF-DUNA", "Vaso Duna", "Casa", 129m, null, BarroCru,
            "Cerâmica queimada a lenha",
            "Feito pelo Estúdio Barro Cru, em Cunha (SP). Argila local e esmalte de cinzas, queimados a lenha a 1.280 °C.",
            Stock: 20),
        new("MF-LINA", "Cadeira Lina", "Casa", 1290m, 1490m, Tora,
            "Freijó de reflorestamento",
            "Feita pela Oficina Tora, em Gonçalves (MG). Freijó de reflorestamento com encaixes sem prego.",
            Stock: 3, ReorderLevel: 5),
        new("MF-GRAO", "Par de canecas Grão", "Cozinha", 89m, null, BarroCru,
            "Cerâmica queimada a lenha · par",
            "Feitas pelo Estúdio Barro Cru, em Cunha (SP). Argila local e esmalte de cinzas, queimados a lenha a 1.280 °C.",
            Stock: 20),
        new("MF-SEIXO", "Jarra Seixo", "Cozinha", 149m, null, BarroCru,
            "Cerâmica queimada a lenha",
            "Feita pelo Estúdio Barro Cru, em Cunha (SP). Argila local e esmalte de cinzas, queimados a lenha a 1.280 °C.",
            Stock: 0),
        new("MF-TRAMA", "Manta Trama", "Têxteis", 219m, null, TearAlto,
            "Algodão e linho tingidos no ateliê",
            "Feita pelo Tear Alto, em Resende Costa (MG). Tecida em tear de pedal manual, com algodão e linho tingidos no ateliê.",
            Stock: 20),
        new("MF-LINHO", "Almofada Linho", "Têxteis", 119m, null, TearAlto,
            "Linho tingido no ateliê",
            "Feita pelo Tear Alto, em Resende Costa (MG). Tecida em tear de pedal manual, com linho tingido no ateliê.",
            Stock: 20),
    ];
}
