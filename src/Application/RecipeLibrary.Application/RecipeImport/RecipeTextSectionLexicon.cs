using System.Text.RegularExpressions;

namespace RecipeLibrary.Application.RecipeImport;

internal static class RecipeTextSectionLexicon
{
    internal static readonly string[] IngredientSectionHeaders = ["ingrediënten", "ingredienten", "benodigdheden", "ingredients"];
    internal static readonly string[] InstructionSectionHeaders = ["bereiding", "werkwijze", "instructies", "stappen", "steps", "instructions", "method", "directions"];
    internal static readonly string[] IntroHeaders = ["inleiding", "beschrijving", "omschrijving", "description"];
    internal static readonly string[] FooterSectionHeaders = ["tips", "tip", "beoordelingen", "reviews", "handig", "veelgestelde vragen", "faq", "gerelateerd", "related", "serveer met", "serving suggestions", "voedingswaarde", "nutrition", "opmerkingen", "comments", "notities", "notes"];
    internal static readonly string[] ChromeLinePrefixes = ["markeer als", "check off", "print recept", "print recipe", "kookstand", "cook mode", "recept opslaan", "save recipe", "of deel", "share via", "direct in je", "raak dit", "stuur dit", "bewaar in", "e-mailadres", "emailadres", "merknamen in", "affiliate", "zet de kookstand", "ga naar de inhoud", "ga naar boven", "naar voorbeeld", "naar mijn", "scroll naar", "laatst bijgewerkt", "gemaakt door", "opgeslagen", "aanmelden", "youtube link"];
    internal static readonly HashSet<string> ChromeExactLines = new(StringComparer.OrdinalIgnoreCase) { "whatsapp", "facebook", "pinterest", "instagram", "tiktok", "youtube", "home", "recepten", "zoeken", "e-mail", "email", "vegan recept", "vegetarisch recept", "lactose arm recept", "lactosevrij recept" };

    internal static readonly Regex MediaFilePattern = new(@"\.(?:png|jpe?g|webp|gif|svg)(?:\b|$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    internal static readonly Regex StepCaptionNoisePattern = new(@"(?:stap|step)\s*\d+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
