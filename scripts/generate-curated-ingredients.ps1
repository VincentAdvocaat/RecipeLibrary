<#
.SYNOPSIS
  Builds data/ingredients/curated-ingredients.json (+ CSV) from the Open Food Facts ingredients taxonomy.

.DESCRIPTION
  RecipeLibrary needs a curated culinary ingredient catalog (Dutch display names + English,
  with aliases) to seed CanonicalIngredient matching. This script:

  1. Downloads (or reuses) the OFF ingredients taxonomy
  2. Keeps entries that have both en: and nl: labels
  3. Drops additives / industrial processing ingredients via heuristics
  4. Prefers short kitchen-oriented names
  5. Merges an explicit manual list of Dutch staples / gaps
  6. Writes JSON + CSV under data/ingredients/

  Documentation (purpose, sources, schema, language keys):
    docs/ingredient-catalog.md

  This script overwrites curated-ingredients.json and .csv only.
  It does not overwrite data/ingredients/README.md or docs/.

.NOTES
  Language keys in the JSON match OFF taxonomy prefixes (en, nl, fr, ...).
  See languageKeys in the output JSON and docs/ingredient-catalog.md.
#>
[CmdletBinding()]
param(
  [string] $OffTaxonomyPath = (Join-Path $env:TEMP 'off-ingredients.txt'),
  [string] $RepoRoot = ''
)

Set-StrictMode -Off
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
  $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

$outDir = Join-Path $RepoRoot 'data\ingredients'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

if (-not (Test-Path $OffTaxonomyPath)) {
  Write-Host "Downloading OFF ingredients taxonomy..."
  Invoke-WebRequest `
    -Uri 'https://raw.githubusercontent.com/openfoodfacts/openfoodfacts-server/refs/heads/main/taxonomies/food/ingredients.txt' `
    -OutFile $OffTaxonomyPath `
    -UseBasicParsing
}

function Parse-Names([string]$csv) {
  $list = New-Object System.Collections.Generic.List[string]
  foreach ($p in ($csv -split ',')) {
    $t = $p.Trim()
    if ($t.Length -gt 0 -and $t -ne '?') { [void]$list.Add($t) }
  }
  # Comma prevents PowerShell from unrolling a single-item list into a string.
  return ,$list
}

# Align with IngredientTextNormalizer: trim, lowercase, collapse whitespace, strip diacritics.
function Get-Norm([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return '' }
  $s = ($s.Trim().ToLowerInvariant() -replace '\s+', ' ')
  $fd = $s.Normalize([Text.NormalizationForm]::FormD)
  $sb = New-Object Text.StringBuilder
  foreach ($ch in $fd.ToCharArray()) {
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
      [void]$sb.Append($ch)
    }
  }
  return ($sb.ToString().Normalize([Text.NormalizationForm]::FormC)).Trim()
}

$denyName = [regex]::new(@'
^(e\d|e-\d)|\badditive\b|\bcolour\b|\bcolor\b|\bpreservative\b|\bemulsifier\b|\bstabiliser\b|\bstabilizer\b|\bthickener\b|\bsweetener\b|\bantioxidant\b|\bflavour\b|\bflavor enhancer\b|\bacidity regulator\b|\bfirming agent\b|\banti-caking\b|\bglazing agent\b|\braising agent\b|\bflour treatment\b|\bsequestrant\b|\bhumectant\b|\bpropellant\b|\bpackaging gas\b|\bmodified starch\b|\bglucose syrup\b|\bmaltodextrin\b|\blecithin\b|\bmono and diglyceride\b|\bpolyglycerol\b|\bcarrageenan\b|\bxanthan\b|\bguar gum\b|\blocust bean\b|\bcellulose\b|\bglycerol\b|\bsorbitol\b|\baspartame\b|\bsaccharin\b|\bcyclamate\b|\bacesulfame\b|\bsteviol\b|\benzyme\b|\brennet\b|\bculture\b|\bferment\b|\bstarter\b|\bbacteria\b|\bbifidus\b|\blactobacillus\b|\blysozyme\b|\bpectin\b|\bcaseinate\b|\bwhey powder\b|\bmilk protein\b|\bsoy protein isolate\b|\bhydrolysed\b|\bhydrogenated\b|\binteresterified\b|\bpalm fat\b|\bvegetable fat\b|\bfiber\b|\bfibre\b|\bextract\b|\boleoresin\b|\bconcentrate\b|\bisolate\b|\bhydrolysate\b|\bprotein powder\b|\bcollagen\b|\bingredient\b|\bpreparation\b|\bfilling\b|\btopping\b|\bcoating\b|\bcrumb\b|\bstarch\b|\bdextrin\b|\bglucose\b|\bfructose syrup\b|\binvert sugar\b|\bcaramelised\b|\bcaramelized\b|\bdisaccharide\b|\bsucrose\b|\bsaccharose\b|\bsacharose\b|\bmaltose\b|\btrehalose\b|\bmelado\b|\bbutterfat\b|\baloe vera\b|\bcottonseed\b|\bpalm olein\b|\bpalm stearin\b|\binulin\b|\bcasein\b|\bwhey protein\b|\bcoffea\b|\bmarinade\b|\bvinaigrette\b|\bkafferkoren\b|\brent milk\b|\banhydrous milk fat\b
'@, 'IgnoreCase')

$denyParent = [regex]::new('additive|e\d{3}|colour|color|preservative|emulsifier|stabiliser|thickener|sweetener|antioxidant|flavouring|flavoring|processing aid|added sugar', 'IgnoreCase')
$preferParent = [regex]::new('vegetable|fruit|herb|spice|meat|poultry|fish|seafood|dairy|cheese|milk|egg|cereal|flour|oil|fat|nut|seed|legume|pulse|mushroom|sugar|salt|vinegar|sauce|condiment|grain|pasta|rice|tea|coffee|chocolate|honey|butter|cream|berry|citrus', 'IgnoreCase')

$lines = [IO.File]::ReadAllLines($OffTaxonomyPath, [Text.UTF8Encoding]::new($false))
$parsed = New-Object System.Collections.Generic.List[object]
$langMap = $null
$parents = $null
$inEntry = $false

function Complete-Entry {
  if (-not $script:inEntry) { return }
  $script:inEntry = $false
  if (-not $script:langMap.ContainsKey('en') -or -not $script:langMap.ContainsKey('nl')) { return }

  $enNames = Parse-Names $script:langMap['en']
  $nlNames = Parse-Names $script:langMap['nl']
  if ($enNames.Count -eq 0 -or $nlNames.Count -eq 0) { return }

  $enPrimary = $enNames[0]
  $nlPrimary = $nlNames[0]
  $id = (Get-Norm $enPrimary) -replace '\s+', '-'
  if (-not $id) { return }

  $allEn = ($enNames -join ' | ')
  if ($denyName.IsMatch($enPrimary) -or $denyName.IsMatch($allEn) -or $denyName.IsMatch($nlPrimary)) { return }
  foreach ($p in $script:parents) {
    if ($denyParent.IsMatch($p)) { return }
  }
  if ($enPrimary.Length -gt 45 -or $nlPrimary.Length -gt 45) { return }
  if ($enPrimary -match '\d{3,}') { return }

  $prefer = $false
  foreach ($p in $script:parents) {
    if ($preferParent.IsMatch($p)) { $prefer = $true }
  }

  $words = @($enPrimary -split '\s+').Count
  $score = 0
  if ($prefer) { $score += 40 }
  if ($words -eq 1) { $score += 35 }
  elseif ($words -eq 2) { $score += 22 }
  elseif ($words -eq 3) { $score += 8 }
  else { $score -= 15 }
  if ($enPrimary.Length -le 14) { $score += 8 }
  if ($nlNames.Count -gt 1) { $score += 4 }
  if ($enPrimary -match ' juice$' -and -not $prefer) { $score -= 20 }

  $parentArr = @($script:parents)
  $enArr = @($enNames)
  $nlArr = @($nlNames)

  [void]$script:parsed.Add([pscustomobject]@{
      id         = $id
      score      = $score
      offParents = $parentArr
      names      = [ordered]@{ en = $enArr; nl = $nlArr }
      enPrimary  = $enPrimary
      nlPrimary  = $nlPrimary
    })
}

foreach ($line in $lines) {
  if ([string]::IsNullOrWhiteSpace($line)) { Complete-Entry; continue }
  if ($line.StartsWith('#')) { continue }

  if ($line -match '^<\s*(.+)$') {
    if (-not $inEntry) {
      $inEntry = $true
      $langMap = @{}
      $parents = New-Object System.Collections.Generic.List[string]
    }
    [void]$parents.Add($Matches[1].Trim())
    continue
  }

  if ($line -match '^([a-z]{2}(?:_[a-z]{2})?|xx):\s*(.+)$') {
    if (-not $inEntry) {
      $inEntry = $true
      $langMap = @{}
      $parents = New-Object System.Collections.Generic.List[string]
    }
    $key = $Matches[1]
    if (-not $langMap.ContainsKey($key)) { $langMap[$key] = $Matches[2].Trim() }
    continue
  }
}
Complete-Entry

$byId = @{}
foreach ($e in ($parsed | Sort-Object score -Descending)) {
  if (-not $byId.ContainsKey($e.id)) { $byId[$e.id] = $e }
}

$byNl = @{}
foreach ($e in ($byId.Values | Sort-Object score -Descending)) {
  $nk = Get-Norm $e.nlPrimary
  if (-not $byNl.ContainsKey($nk)) { $byNl[$nk] = $e }
}
$unique = @($byNl.Values)
Write-Host "Clean OFF en+nl candidates: $($unique.Count)"

$byEn = @{}
foreach ($e in $unique) {
  foreach ($n in $e.names.en) {
    $k = Get-Norm $n
    if (-not $byEn.ContainsKey($k)) { $byEn[$k] = $e }
  }
}

$stapleQueries = @(
  'tomato', 'onion', 'garlic', 'carrot', 'potato', 'cucumber', 'lettuce', 'spinach', 'broccoli', 'cauliflower',
  'courgette', 'zucchini', 'aubergine', 'eggplant', 'leek', 'celery', 'mushroom', 'cabbage', 'kale', 'beetroot',
  'radish', 'pumpkin', 'sweet potato', 'corn', 'pea', 'green bean', 'asparagus', 'artichoke', 'avocado', 'pepper',
  'chilli pepper', 'chili pepper', 'bell pepper', 'apple', 'banana', 'orange', 'lemon', 'lime', 'strawberry',
  'blueberry', 'raspberry', 'grape', 'pear', 'peach', 'mango', 'pineapple', 'kiwi', 'cherry', 'watermelon', 'melon',
  'fig', 'date', 'raisin', 'coconut', 'milk', 'butter', 'cream', 'cheese', 'yoghurt', 'yogurt', 'egg', 'quark',
  'chicken', 'pork', 'lamb', 'turkey', 'veal', 'bacon', 'ham', 'salmon', 'tuna', 'cod', 'shrimp', 'prawn', 'mussel',
  'rice', 'pasta', 'wheat flour', 'flour', 'bread', 'oat', 'couscous', 'quinoa', 'barley', 'noodle', 'spaghetti',
  'olive oil', 'sunflower oil', 'rapeseed oil', 'coconut oil', 'sesame oil', 'vinegar', 'balsamic vinegar',
  'soy sauce', 'soya sauce', 'mustard', 'mayonnaise', 'ketchup', 'tomato puree', 'tomato paste', 'coconut milk',
  'honey', 'sugar', 'salt', 'black pepper', 'white pepper', 'basil', 'parsley', 'thyme', 'oregano', 'rosemary',
  'mint', 'dill', 'coriander', 'cumin', 'cinnamon', 'turmeric', 'ginger', 'nutmeg', 'clove', 'bay leaf', 'cardamom',
  'almond', 'walnut', 'peanut', 'cashew', 'hazelnut', 'pistachio', 'sesame', 'chickpea', 'lentil', 'bean', 'tofu',
  'water', 'red wine', 'white wine', 'beer', 'coffee', 'tea', 'cocoa', 'chocolate', 'vanilla', 'yeast', 'baking powder'
)

$selected = @{}
foreach ($e in ($unique | Sort-Object score -Descending)) {
  $words = @($e.enPrimary -split '\s+').Count
  if ($e.score -ge 40 -and $words -le 3) {
    $selected[$e.id] = $e
  }
  if ($selected.Count -ge 520) { break }
}

foreach ($s in $stapleQueries) {
  $k = Get-Norm $s
  if ($byEn.ContainsKey($k)) { $selected[$byEn[$k].id] = $byEn[$k] }
}

# Manual NL kitchen staples / gaps (en + nl). First name is preferred display form.
# Also used as culinary overrides when OFF has awkward primaries (e.g. Pastas, varken).
$manual = @(
  @{ id = 'beef'; en = @('beef'); nl = @('rundvlees', 'rund') },
  @{ id = 'pork'; en = @('pork'); nl = @('varkensvlees', 'varken') },
  @{ id = 'veal'; en = @('veal'); nl = @('kalfsvlees', 'kalf') },
  @{ id = 'minced-meat'; en = @('minced meat', 'ground meat'); nl = @('gehakt') },
  @{ id = 'minced-beef'; en = @('minced beef', 'ground beef'); nl = @('rundergehakt') },
  @{ id = 'minced-pork'; en = @('minced pork'); nl = @('varkensgehakt') },
  @{ id = 'half-om-half-mince'; en = @('half and half mince', 'mixed mince'); nl = @('half-om-half gehakt', 'half om half gehakt') },
  @{ id = 'chicken-thigh'; en = @('chicken thigh'); nl = @('kippendij') },
  @{ id = 'chicken-drumstick'; en = @('chicken drumstick'); nl = @('kippenbout') },
  @{ id = 'pork-fillet'; en = @('pork fillet', 'pork tenderloin'); nl = @('varkenshaas') },
  @{ id = 'sausage'; en = @('sausage'); nl = @('worst') },
  @{ id = 'fresh-sausage'; en = @('fresh sausage', 'frying sausage'); nl = @('braadworst') },
  @{ id = 'oats'; en = @('oats', 'oat'); nl = @('havermout', 'haver', 'havervlokken') },
  @{ id = 'paprika-spice'; en = @('paprika', 'ground paprika'); nl = @('paprikapoeder', 'paprika') },
  @{ id = 'chili-powder'; en = @('chili powder', 'chilli powder'); nl = @('chilipoeder') },
  @{ id = 'garlic-powder'; en = @('garlic powder'); nl = @('knoflookpoeder') },
  @{ id = 'onion-powder'; en = @('onion powder'); nl = @('uienpoeder') },
  @{ id = 'saffron'; en = @('saffron'); nl = @('saffraan') },
  @{ id = 'tempeh'; en = @('tempeh'); nl = @('tempeh') },
  @{ id = 'seitan'; en = @('seitan'); nl = @('seitan') },
  @{ id = 'edamame'; en = @('edamame'); nl = @('edamame') },
  @{ id = 'baking-soda'; en = @('baking soda', 'sodium bicarbonate', 'bicarbonate of soda'); nl = @('natriumbicarbonaat', 'baking soda') },
  @{ id = 'gelatine'; en = @('gelatine', 'gelatin'); nl = @('gelatine') },
  @{ id = 'agar'; en = @('agar', 'agar-agar', 'agar agar'); nl = @('agar', 'agar-agar') },
  @{ id = 'courgette'; en = @('courgette', 'zucchini'); nl = @('courgette', 'zucchini') },
  @{ id = 'spring-onion'; en = @('spring onion', 'green onion', 'scallion'); nl = @('lente-ui', 'bosui') },
  @{ id = 'shallot'; en = @('shallot'); nl = @('sjalot') },
  @{ id = 'red-onion'; en = @('red onion'); nl = @('rode ui') },
  @{ id = 'celery'; en = @('celery', 'stalk celery'); nl = @('bleekselderij', 'selderij') },
  @{ id = 'celeriac'; en = @('celeriac', 'celery root'); nl = @('knolselderij') },
  @{ id = 'swede'; en = @('swede', 'rutabaga'); nl = @('koolraap') },
  @{ id = 'kohlrabi'; en = @('kohlrabi'); nl = @('koolrabi') },
  # Dutch kitchen: witlof ≠ andijvie (OFF often conflates them under "endive").
  @{ id = 'endive'; en = @('Belgian endive', 'witloof', 'chicory'); nl = @('witlof', 'witloof') },
  @{ id = 'andijvie'; en = @('curly endive', 'frisee', 'endive'); nl = @('andijvie') },
  @{ id = 'lambs-lettuce'; en = @("lamb's lettuce", 'corn salad', 'mache'); nl = @('veldsla') },
  @{ id = 'rocket'; en = @('rocket', 'arugula', 'rucola'); nl = @('rucola') },
  @{ id = 'iceberg-lettuce'; en = @('iceberg lettuce'); nl = @('ijsbergsla') },
  @{ id = 'watercress'; en = @('watercress'); nl = @('waterkers') },
  @{ id = 'chinese-cabbage'; en = @('Chinese cabbage', 'napa cabbage'); nl = @('Chinese kool') },
  @{ id = 'red-cabbage'; en = @('red cabbage'); nl = @('rode kool') },
  @{ id = 'white-cabbage'; en = @('white cabbage'); nl = @('witte kool') },
  @{ id = 'pointed-cabbage'; en = @('pointed cabbage', 'hispi cabbage'); nl = @('spitskool') },
  @{ id = 'brussels-sprout'; en = @('brussels sprout', 'brussels sprouts'); nl = @('spruitjes', 'spruit') },
  @{ id = 'pak-choi'; en = @('pak choi', 'bok choy', 'paksoi'); nl = @('paksoi', 'pak choi', 'bok choy') },
  @{ id = 'bean-sprouts'; en = @('bean sprouts'); nl = @('tauge', 'taugé') },
  @{ id = 'cherry-tomato'; en = @('cherry tomato', 'cherry tomatoes'); nl = @('cherrytomaat', 'cherry tomaat') },
  @{ id = 'champignon'; en = @('button mushroom', 'white mushroom', 'mushroom'); nl = @('champignon', 'champignons') },
  @{ id = 'oyster-mushroom'; en = @('oyster mushroom'); nl = @('oesterzwam') },
  @{ id = 'chestnut-mushroom'; en = @('chestnut mushroom', 'cremini', 'chestnut mushrooms'); nl = @('kastanjechampignon') },
  @{ id = 'shiitake'; en = @('shiitake'); nl = @('shiitake') },
  @{ id = 'stock-cube'; en = @('stock cube', 'bouillon cube'); nl = @('bouillonblokje') },
  @{ id = 'stock'; en = @('stock', 'broth'); nl = @('bouillon') },
  @{ id = 'vegetable-stock'; en = @('vegetable stock', 'vegetable broth'); nl = @('groentebouillon') },
  @{ id = 'beef-stock'; en = @('beef stock', 'beef broth'); nl = @('vleesbouillon') },
  @{ id = 'chicken-broth'; en = @('chicken broth', 'chicken stock'); nl = @('kippenbouillon') },
  @{ id = 'creme-fraiche'; en = @('creme fraiche', 'crème fraîche'); nl = @('crème fraîche', 'creme fraiche') },
  @{ id = 'smoked-salmon'; en = @('smoked salmon'); nl = @('gerookte zalm') },
  @{ id = 'icing-sugar'; en = @('icing sugar', 'powdered sugar'); nl = @('poedersuiker') },
  @{ id = 'brown-sugar'; en = @('brown sugar', 'muscovado', 'muscovado sugar'); nl = @('bruine suiker', 'basterdsuiker') },
  @{ id = 'self-raising-flour'; en = @('self-raising flour', 'self rising flour'); nl = @('zelfrijzend bakmeel') },
  @{ id = 'cornflour'; en = @('cornflour', 'corn starch', 'cornstarch'); nl = @('maizena', 'maïzena', 'maiszetmeel') },
  @{ id = 'double-cream'; en = @('double cream', 'heavy cream', 'whipping cream'); nl = @('slagroom') },
  @{ id = 'margarine'; en = @('margarine'); nl = @('margarine') },
  @{ id = 'fish-sauce'; en = @('fish sauce'); nl = @('vissaus') },
  @{ id = 'oyster-sauce'; en = @('oyster sauce'); nl = @('oestersaus') },
  @{ id = 'worcestershire-sauce'; en = @('worcestershire sauce'); nl = @('worcestersaus') },
  @{ id = 'tabasco'; en = @('tabasco', 'tabasco sauce'); nl = @('tabasco') },
  @{ id = 'harissa'; en = @('harissa'); nl = @('harissa') },
  @{ id = 'pesto'; en = @('pesto'); nl = @('pesto') },
  @{ id = 'hummus'; en = @('hummus', 'houmous'); nl = @('hummus') },
  @{ id = 'passata'; en = @('passata', 'tomato passata'); nl = @('passata') },
  @{ id = 'chopped-tomato'; en = @('chopped tomatoes', 'diced tomatoes'); nl = @('tomatenblokjes') },
  @{ id = 'sun-dried-tomato'; en = @('sun-dried tomato', 'sun dried tomatoes'); nl = @('zongedroogde tomaat', 'zongedroogde tomaten') },
  @{ id = 'phyllo'; en = @('phyllo', 'filo pastry'); nl = @('filodeeg') },
  @{ id = 'puff-pastry'; en = @('puff pastry'); nl = @('bladerdeeg') },
  @{ id = 'shortcrust-pastry'; en = @('shortcrust pastry'); nl = @('kruimeldeeg') },
  @{ id = 'pasta'; en = @('pasta'); nl = @('pasta', 'deegwaren') },
  @{ id = 'spaghetti'; en = @('spaghetti'); nl = @('spaghetti') },
  @{ id = 'macaroni'; en = @('macaroni'); nl = @('macaroni') },
  @{ id = 'fusilli'; en = @('fusilli'); nl = @('fusilli') },
  @{ id = 'tagliatelle'; en = @('tagliatelle'); nl = @('tagliatelle') },
  @{ id = 'lasagne-sheet'; en = @('lasagne sheet', 'lasagna sheet', 'lasagne sheets'); nl = @('lasagnebladen', 'lasagneblad') },
  @{ id = 'tortilla'; en = @('tortilla'); nl = @('tortilla') },
  @{ id = 'wrap'; en = @('wrap', 'tortilla wrap'); nl = @('wrap') },
  @{ id = 'pita'; en = @('pita', 'pitta'); nl = @('pita', 'pitabroodje') },
  @{ id = 'naan'; en = @('naan'); nl = @('naan') },
  @{ id = 'baguette'; en = @('baguette', 'french stick'); nl = @('stokbrood') },
  @{ id = 'polenta'; en = @('polenta'); nl = @('polenta') },
  @{ id = 'bulgur'; en = @('bulgur', 'bulghur'); nl = @('bulgur') },
  @{ id = 'miso'; en = @('miso'); nl = @('miso') },
  @{ id = 'nori'; en = @('nori'); nl = @('nori') },
  @{ id = 'wasabi'; en = @('wasabi'); nl = @('wasabi') },
  @{ id = 'rice-vinegar'; en = @('rice vinegar'); nl = @('rijstazijn') },
  @{ id = 'apple-cider-vinegar'; en = @('apple cider vinegar', 'cider vinegar'); nl = @('appelazijn', 'ciderazijn') },
  @{ id = 'mirin'; en = @('mirin'); nl = @('mirin') },
  @{ id = 'rice-noodles'; en = @('rice noodles'); nl = @('rijstnoedels') },
  @{ id = 'lemongrass'; en = @('lemongrass', 'lemon grass'); nl = @('citroengras', 'sereh') },
  @{ id = 'galangal'; en = @('galangal'); nl = @('galanga', 'laos') },
  @{ id = 'star-anise'; en = @('star anise'); nl = @('steranijs') },
  @{ id = 'fennel-seed'; en = @('fennel seed'); nl = @('venkelzaad') },
  @{ id = 'mustard-seed'; en = @('mustard seed'); nl = @('mosterdzaad') },
  @{ id = 'poppy-seed'; en = @('poppy seed'); nl = @('maanzaad') },
  @{ id = 'pumpkin-seed'; en = @('pumpkin seed'); nl = @('pompoenpit') },
  @{ id = 'pine-nut'; en = @('pine nut', 'pine nuts', 'pinenuts'); nl = @('pijnboompit', 'pijnboompitten') },
  @{ id = 'sesame'; en = @('sesame', 'sesame seed', 'sesame seeds'); nl = @('sesamzaad', 'sesam', 'sesamzaadjes') },
  @{ id = 'chestnut'; en = @('chestnut'); nl = @('kastanje') },
  @{ id = 'cranberry'; en = @('cranberry'); nl = @('cranberry', 'veenbes') },
  @{ id = 'pomegranate'; en = @('pomegranate'); nl = @('granaatappel') },
  @{ id = 'passion-fruit'; en = @('passion fruit'); nl = @('passievrucht') },
  @{ id = 'nectarine'; en = @('nectarine'); nl = @('nectarine') },
  @{ id = 'plum'; en = @('plum'); nl = @('pruim') },
  @{ id = 'grapefruit'; en = @('grapefruit'); nl = @('grapefruit') },
  @{ id = 'mandarin'; en = @('mandarin', 'tangerine', 'mandarine', 'mandarin orange'); nl = @('mandarijn', 'mandarijnen') },
  @{ id = 'clementine'; en = @('clementine', 'clementines'); nl = @('clementine') },
  @{ id = 'blueberry'; en = @('blueberry', 'blueberries'); nl = @('blauwe bes', 'bosbes', 'bosbessen') },
  @{ id = 'rhubarb'; en = @('rhubarb'); nl = @('rabarber') },
  @{ id = 'redcurrant'; en = @('redcurrant', 'red currant'); nl = @('aalbessen', 'rode bes') },
  @{ id = 'blackcurrant'; en = @('blackcurrant', 'black currant'); nl = @('zwarte bes') },
  @{ id = 'blackberry'; en = @('blackberry'); nl = @('braam') },
  @{ id = 'trout'; en = @('trout'); nl = @('forel') },
  @{ id = 'mackerel'; en = @('mackerel'); nl = @('makreel') },
  @{ id = 'herring'; en = @('herring'); nl = @('haring') },
  @{ id = 'anchovy'; en = @('anchovy'); nl = @('ansjovis') },
  @{ id = 'sardine'; en = @('sardine'); nl = @('sardine') },
  @{ id = 'sea-bass'; en = @('sea bass'); nl = @('zeebaars') },
  @{ id = 'mussel'; en = @('mussel', 'mussels'); nl = @('mossel', 'mosselen') },
  @{ id = 'crab'; en = @('crab'); nl = @('krab') },
  @{ id = 'lobster'; en = @('lobster'); nl = @('kreeft') },
  @{ id = 'squid'; en = @('squid', 'calamari'); nl = @('inktvis', 'calamari') },
  @{ id = 'scallop'; en = @('scallop'); nl = @('sint-jakobsschelp', 'coquille') },
  @{ id = 'duck'; en = @('duck'); nl = @('eend') },
  @{ id = 'rabbit'; en = @('rabbit'); nl = @('konijn') },
  @{ id = 'chorizo'; en = @('chorizo'); nl = @('chorizo') },
  @{ id = 'salami'; en = @('salami'); nl = @('salami') },
  @{ id = 'prosciutto'; en = @('prosciutto', 'parma ham'); nl = @('parmaham', 'prosciutto') },
  @{ id = 'pancetta'; en = @('pancetta'); nl = @('pancetta') },
  @{ id = 'mozzarella'; en = @('mozzarella'); nl = @('mozzarella') },
  @{ id = 'parmesan'; en = @('parmesan', 'parmigiano reggiano'); nl = @('parmezaan', 'parmigiano') },
  @{ id = 'pecorino'; en = @('pecorino', 'pecorino romano'); nl = @('pecorino') },
  @{ id = 'feta'; en = @('feta'); nl = @('feta') },
  @{ id = 'goat-cheese'; en = @('goat cheese'); nl = @('geitenkaas') },
  @{ id = 'ricotta'; en = @('ricotta'); nl = @('ricotta') },
  @{ id = 'mascarpone'; en = @('mascarpone'); nl = @('mascarpone') },
  @{ id = 'brie'; en = @('brie'); nl = @('brie') },
  @{ id = 'camembert'; en = @('camembert'); nl = @('camembert') },
  @{ id = 'gouda'; en = @('gouda'); nl = @('gouda', 'Goudse kaas') },
  @{ id = 'cheddar'; en = @('cheddar'); nl = @('cheddar') },
  @{ id = 'blue-cheese'; en = @('blue cheese', 'gorgonzola', 'roquefort'); nl = @('blauwaderkaas', 'gorgonzola', 'roquefort') },
  @{ id = 'halloumi'; en = @('halloumi'); nl = @('halloumi') },
  @{ id = 'gruyere'; en = @('gruyere', 'gruyère'); nl = @('gruyère') },
  @{ id = 'emmental'; en = @('emmental', 'emmentaler'); nl = @('emmentaler') },
  @{ id = 'paneer'; en = @('paneer'); nl = @('paneer') },
  @{ id = 'postelein'; en = @('purslane'); nl = @('postelein') },
  @{ id = 'snijbiet'; en = @('chard', 'Swiss chard'); nl = @('snijbiet') },
  @{ id = 'raapstelen'; en = @('turnip greens'); nl = @('raapstelen') },
  @{ id = 'kapucijners'; en = @('marrowfat peas'); nl = @('kapucijners') },
  @{ id = 'brown-beans'; en = @('brown beans'); nl = @('bruine bonen') },
  @{ id = 'white-beans'; en = @('white beans', 'cannellini'); nl = @('witte bonen') },
  @{ id = 'kidney-bean'; en = @('kidney bean', 'red kidney bean', 'red kidney beans'); nl = @('kidneybonen', 'rode kidneybonen') },
  @{ id = 'split-peas'; en = @('split peas', 'split pea'); nl = @('spliterwten', 'spliterwt') },
  @{ id = 'pea'; en = @('pea', 'peas', 'garden pea'); nl = @('erwt', 'erwten', 'doperwt', 'doperwten') },
  @{ id = 'sugar-snap-pea'; en = @('sugar snap pea', 'sugar snaps', 'mangetout'); nl = @('sugar snaps', 'peultjes') },
  @{ id = 'new-potato'; en = @('new potato', 'baby potato'); nl = @('krieltjes', 'krieltje') },
  @{ id = 'sweet-potato'; en = @('sweet potato'); nl = @('zoete aardappel', 'bataat') },
  @{ id = 'spek'; en = @('speck', 'smoked bacon', 'bacon'); nl = @('spek', 'rookspek', 'ontbijtspek', 'bacon') },
  @{ id = 'rookworst'; en = @('smoked sausage'); nl = @('rookworst') },
  @{ id = 'stroop'; en = @('syrup', 'treacle'); nl = @('stroop') },
  @{ id = 'apple-syrup'; en = @('apple syrup'); nl = @('appelstroop') },
  @{ id = 'maple-syrup'; en = @('maple syrup'); nl = @('ahornsiroop') },
  @{ id = 'jam'; en = @('jam', 'preserve'); nl = @('jam', 'confituur') },
  @{ id = 'apple-sauce'; en = @('apple sauce', 'applesauce'); nl = @('appelmoes') },
  @{ id = 'pindakaas'; en = @('peanut butter'); nl = @('pindakaas') },
  @{ id = 'desiccated-coconut'; en = @('desiccated coconut', 'shredded coconut'); nl = @('kokosrasp') },
  @{ id = 'breadcrumb'; en = @('breadcrumb', 'breadcrumbs'); nl = @('paneermeel') },
  @{ id = 'panko'; en = @('panko', 'panko breadcrumbs'); nl = @('panko') },
  @{ id = 'dijon-mustard'; en = @('dijon mustard'); nl = @('dijonmosterd') },
  @{ id = 'caper'; en = @('caper', 'capers'); nl = @('kappertjes', 'kappertje') },
  @{ id = 'gherkin'; en = @('gherkin', 'pickle', 'pickles'); nl = @('augurk', 'augurken') },
  @{ id = 'cocoa'; en = @('cocoa', 'cocoa powder'); nl = @('cacaopoeder', 'cacao') },
  @{ id = 'vanilla'; en = @('vanilla'); nl = @('vanille') },
  @{ id = 'vanilla-pod'; en = @('vanilla pod', 'vanilla bean'); nl = @('vanillestokje') },
  @{ id = 'vanilla-extract'; en = @('vanilla extract'); nl = @('vanille-extract', 'vanille extract') },
  @{ id = 'almond-milk'; en = @('almond milk'); nl = @('amandelmelk') },
  @{ id = 'oat-milk'; en = @('oat milk'); nl = @('havermelk') },
  @{ id = 'soy-milk'; en = @('soy milk', 'soya milk'); nl = @('sojamelk') },
  @{ id = 'cider'; en = @('cider'); nl = @('cider', 'appelcider') },
  @{ id = 'juniper-berry'; en = @('juniper berry'); nl = @('jeneverbes') },
  @{ id = 'mace'; en = @('mace'); nl = @('foelie') },
  @{ id = 'coriander-seed'; en = @('coriander seed'); nl = @('korianderzaad') },
  @{ id = 'fenugreek'; en = @('fenugreek'); nl = @('fenegriek') },
  @{ id = 'sumac'; en = @('sumac'); nl = @('sumak', 'sumac') },
  @{ id = 'ras-el-hanout'; en = @('ras el hanout'); nl = @('ras el hanout') },
  @{ id = 'zaatar'; en = @('zaatar', "za'atar"); nl = @('zaatar', "za'atar") },
  @{ id = 'tahini'; en = @('tahini', 'tahina'); nl = @('tahini', 'tahin') },
  @{ id = 'sambal'; en = @('sambal', 'sambal oelek'); nl = @('sambal', 'sambal oelek') },
  @{ id = 'sambal-badjak'; en = @('sambal badjak'); nl = @('sambal badjak') },
  @{ id = 'kroepoek'; en = @('prawn cracker', 'shrimp cracker'); nl = @('kroepoek') },
  # ketjap ≠ sojasaus: do not let OFF attach "ketjap" as a soy-sauce alias.
  @{ id = 'ketjap-manis'; en = @('kecap manis', 'sweet soy sauce', 'ketjap'); nl = @('ketjap', 'ketjap manis') },
  @{ id = 'soy-sauce'; en = @('soy sauce', 'soya sauce'); nl = @('sojasaus') },
  @{ id = 'shrimp-paste'; en = @('shrimp paste', 'trassi'); nl = @('trassi', 'terasi') },
  @{ id = 'chia-seed'; en = @('chia seed', 'chia seeds', 'chia'); nl = @('chiazaad', 'chia') },
  @{ id = 'linseed'; en = @('linseed', 'flax seed'); nl = @('lijnzaad') },
  @{ id = 'sunflower-seed'; en = @('sunflower seed'); nl = @('zonnebloempit') },
  @{ id = 'sorghum'; en = @('sorghum'); nl = @('sorghum') },
  @{ id = 'kaffir-lime'; en = @('makrut lime', 'kaffir lime'); nl = @('makrut limoen') },
  @{ id = 'avocado'; en = @('avocado'); nl = @('avocado') },
  @{ id = 'lime'; en = @('lime'); nl = @('limoen', 'lime') },
  @{ id = 'oregano'; en = @('oregano'); nl = @('oregano') },
  @{ id = 'quinoa'; en = @('quinoa'); nl = @('quinoa') },
  @{ id = 'spelt'; en = @('spelt'); nl = @('spelt') },
  @{ id = 'amaranth'; en = @('amaranth'); nl = @('amaranth', 'amarant') },
  @{ id = 'curry'; en = @('curry', 'curry powder'); nl = @('kerrie', 'curry') },
  @{ id = 'ketchup'; en = @('ketchup'); nl = @('ketchup', 'tomatenketchup') },
  @{ id = 'ghee'; en = @('ghee'); nl = @('ghee', 'ghi') },
  @{ id = 'okra'; en = @('okra'); nl = @('okra') },
  @{ id = 'smetana'; en = @('smetana'); nl = @('smetana') }
)

function Get-NameList([object]$value) {
  $list = New-Object System.Collections.Generic.List[string]
  if ($null -eq $value) { return ,$list.ToArray() }
  # A scalar string must not be enumerated char-by-char.
  if ($value -is [string]) {
    if (-not [string]::IsNullOrWhiteSpace($value)) { [void]$list.Add($value.Trim()) }
    return ,$list.ToArray()
  }
  foreach ($x in @($value)) {
    if ($null -eq $x) { continue }
    if ($x -is [string]) {
      if (-not [string]::IsNullOrWhiteSpace($x)) { [void]$list.Add($x.Trim()) }
      continue
    }
    foreach ($y in (Get-NameList $x)) { [void]$list.Add($y) }
  }
  return ,$list.ToArray()
}

function Merge-Names([object]$existing, [object]$extra) {
  $list = New-Object System.Collections.Generic.List[string]
  $seen = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($group in @($existing, $extra)) {
    foreach ($x in (Get-NameList $group)) {
      $n = Get-Norm $x
      if ($n.Length -eq 0) { continue }
      if ($seen.Add($n)) { [void]$list.Add($x) }
    }
  }
  return ,$list.ToArray()
}

function New-StringList([object]$values) {
  $list = New-Object System.Collections.Generic.List[string]
  foreach ($x in (Get-NameList $values)) { [void]$list.Add($x) }
  return $list
}

foreach ($m in $manual) {
  if ($selected.ContainsKey($m.id)) {
    $ex = $selected[$m.id]
    # Manual names win primary position (Merge-Names keeps first-seen).
    $enMerged = New-StringList (Merge-Names $m.en $ex.names.en)
    $nlMerged = New-StringList (Merge-Names $m.nl $ex.names.nl)
    $selected[$m.id] = [pscustomobject]@{
      id         = $m.id
      score      = [Math]::Max([int]$ex.score, 100)
      offParents = @($ex.offParents)
      names      = [ordered]@{ en = $enMerged; nl = $nlMerged }
      enPrimary  = $m.en[0]
      nlPrimary  = $m.nl[0]
    }
  }
  else {
    $selected[$m.id] = [pscustomobject]@{
      id         = $m.id
      score      = 100
      offParents = @()
      names      = [ordered]@{ en = (New-StringList $m.en); nl = (New-StringList $m.nl) }
      enPrimary  = $m.en[0]
      nlPrimary  = $m.nl[0]
    }
  }
}

# Strip culinary-incorrect aliases that OFF often attaches to the wrong ingredient.
$aliasStripById = @{
  'soy-sauce'   = @('ketjap', 'ketjap manis', 'kecap manis')
  'soya-sauce'  = @('ketjap', 'ketjap manis', 'kecap manis')
  'andijvie'    = @('witlof', 'witloof', 'Belgian endive', 'belgian endive')
  'endive'      = @('andijvie', 'curly endive', 'frisée', 'frisee')
  'mandarin'    = @('clementine', 'clementines', 'clementins')
  'sorghum'     = @('kafferkoren')
  'kaffir-lime' = @('Kafferlimoen', 'kafferlimoen', 'kaffir limoen')
  'chorizo'     = @('Spicy pork sausage with red pepper no precision')
  'shiitake'    = @('lentinula edodes', 'Lentinula edodes', 'lentinus edodes', 'Lentinus edodes')
  'champignon'  = @('Agaricus bisporus', 'agaricus bisporus')
  'pasta'       = @('Pastas', 'pastas')
}

foreach ($id in @($selected.Keys)) {
  if (-not $aliasStripById.ContainsKey($id)) { continue }
  $strip = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($s in $aliasStripById[$id]) { [void]$strip.Add((Get-Norm $s)) }
  $e = $selected[$id]
  $nl = New-StringList ((Get-NameList $e.names.nl) | Where-Object { -not $strip.Contains((Get-Norm $_)) })
  $en = New-StringList ((Get-NameList $e.names.en) | Where-Object { -not $strip.Contains((Get-Norm $_)) })
  if ($nl.Count -eq 0) { continue }
  $selected[$id] = [pscustomobject]@{
    id         = $e.id
    score      = $e.score
    offParents = @($e.offParents)
    names      = [ordered]@{ en = $en; nl = $nl }
    enPrimary  = $(if ($en.Count -gt 0) { $en[0] } else { $e.enPrimary })
    nlPrimary  = $nl[0]
  }
}

# Drop non-culinary / industrial / confusing OFF rows that slipped past heuristics.
$dropIds = @(
  'disaccharide', 'sucrose', 'maltose', 'trehalose', 'melado',
  'butterfat', 'aloe-vera', 'palm', 'palm-olein', 'palm-stearin',
  'cottonseed-oil', 'marinade', 'vinaigrette', 'coffea-robusta',
  'british-cream', 'rice-sourdough', 'casein', 'inulin', 'whey-protein',
  'must', 'chicory', 'straw-mushroom', 'oarfish', 'weever',
  'fish-oil', 'whey', 'palm-sugar', 'bacon', 'cider-vinegar'
)
foreach ($dropId in $dropIds) {
  if ($selected.ContainsKey($dropId)) {
    $selected.Remove($dropId)
  }
}

function Resolve-UniqueCatalog([object[]]$entries) {
  $claimed = @{} # normalized name -> owner id
  $result = [ordered]@{}
  $mergeLog = New-Object System.Collections.Generic.List[string]
  $dropLog = New-Object System.Collections.Generic.List[string]

  foreach ($e in ($entries | Sort-Object @{ Expression = 'score'; Descending = $true }, @{ Expression = 'id' })) {
    $nlNames = Get-NameList $e.names.nl
    if ($nlNames.Count -eq 0) { continue }
    $primaryNorm = Get-Norm $nlNames[0]
    if ([string]::IsNullOrWhiteSpace($primaryNorm)) { continue }

    if ($claimed.ContainsKey($primaryNorm)) {
      $ownerId = $claimed[$primaryNorm]
      $owner = $result[$ownerId]
      $extraEn = New-Object System.Collections.Generic.List[string]
      $extraNl = New-Object System.Collections.Generic.List[string]
      $nlSet = New-Object 'System.Collections.Generic.HashSet[string]'
      foreach ($n in (Get-NameList $e.names.nl)) { [void]$nlSet.Add((Get-Norm $n)) }

      foreach ($n in @((Get-NameList $e.names.nl) + (Get-NameList $e.names.en))) {
        $nk = Get-Norm $n
        if ([string]::IsNullOrWhiteSpace($nk)) { continue }
        if ($claimed.ContainsKey($nk) -and $claimed[$nk] -ne $ownerId) {
          [void]$dropLog.Add("Dropped '$n' while merging '$($e.id)' into '$ownerId' (owned by '$($claimed[$nk])').")
          continue
        }
        $claimed[$nk] = $ownerId
        if ($nlSet.Contains($nk)) { [void]$extraNl.Add($n) }
        else { [void]$extraEn.Add($n) }
      }
      $mergedEn = New-StringList (Merge-Names $owner.names.en $extraEn)
      $mergedNl = New-StringList (Merge-Names $owner.names.nl $extraNl)
      # Keep bilingual display when EN equals NL primary after merge.
      if ($mergedEn.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$owner.enPrimary)) {
        $mergedEn = New-StringList @([string]$owner.enPrimary)
      }
      $result[$ownerId] = [pscustomobject]@{
        id         = $ownerId
        score      = [Math]::Max([int]$owner.score, [int]$e.score)
        offParents = @($owner.offParents + $e.offParents | Select-Object -Unique)
        names      = [ordered]@{ en = $mergedEn; nl = $mergedNl }
        enPrimary  = $(if ($mergedEn.Count -gt 0) { $mergedEn[0] } else { $owner.enPrimary })
        nlPrimary  = $mergedNl[0]
      }
      [void]$mergeLog.Add("Merged '$($e.id)' into '$ownerId' (shared NL primary '$($nlNames[0])').")
      continue
    }

    $claimed[$primaryNorm] = $e.id
    $cleanNl = New-Object System.Collections.Generic.List[string]
    $cleanEn = New-Object System.Collections.Generic.List[string]
    [void]$cleanNl.Add($nlNames[0])

    foreach ($alias in ($nlNames | Select-Object -Skip 1)) {
      $nk = Get-Norm $alias
      if ([string]::IsNullOrWhiteSpace($nk) -or $nk -eq $primaryNorm) { continue }
      if ($claimed.ContainsKey($nk)) {
        [void]$dropLog.Add("Dropped NL alias '$alias' from '$($e.id)' (owned by '$($claimed[$nk])').")
        continue
      }
      $claimed[$nk] = $e.id
      [void]$cleanNl.Add($alias)
    }

    foreach ($alias in (Get-NameList $e.names.en)) {
      $nk = Get-Norm $alias
      if ([string]::IsNullOrWhiteSpace($nk)) { continue }
      if ($nk -eq $primaryNorm -or ($claimed.ContainsKey($nk) -and $claimed[$nk] -eq $e.id)) {
        # Same owner already (NL primary/alias): keep EN for bilingual display.
        $already = $false
        foreach ($existing in $cleanEn) {
          if ((Get-Norm $existing) -eq $nk) { $already = $true; break }
        }
        if (-not $already) { [void]$cleanEn.Add($alias) }
        continue
      }
      if ($claimed.ContainsKey($nk)) {
        [void]$dropLog.Add("Dropped EN alias '$alias' from '$($e.id)' (owned by '$($claimed[$nk])').")
        continue
      }
      $claimed[$nk] = $e.id
      [void]$cleanEn.Add($alias)
    }

    if ($cleanEn.Count -eq 0) {
      $fallbackEn = Get-NameList $e.names.en
      if ($fallbackEn.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$e.enPrimary)) {
        $fallbackEn = @([string]$e.enPrimary)
      }
      if ($fallbackEn.Count -gt 0) {
        [void]$cleanEn.Add($fallbackEn[0])
      }
    }

    $result[$e.id] = [pscustomobject]@{
      id         = $e.id
      score      = $e.score
      offParents = @($e.offParents)
      names      = [ordered]@{ en = $cleanEn; nl = $cleanNl }
      enPrimary  = $(if ($cleanEn.Count -gt 0) { $cleanEn[0] } else { $e.enPrimary })
      nlPrimary  = $cleanNl[0]
    }
  }

  # Validate: every normalized name maps to exactly one owner; no primary/alias cross-dupes.
  # Note: the same normalized string may appear in both en and nl for one id (bilingual identical names).
  $seen = @{}
  foreach ($e in $result.Values) {
    foreach ($n in @((Get-NameList $e.names.nl) + (Get-NameList $e.names.en))) {
      $nk = Get-Norm $n
      if ([string]::IsNullOrWhiteSpace($nk)) { continue }
      if ($seen.ContainsKey($nk) -and $seen[$nk] -ne $e.id) {
        throw "Catalog uniqueness violated: '$n' claimed by both '$($seen[$nk])' and '$($e.id)'."
      }
      $seen[$nk] = $e.id
    }
  }

  # Final safety: never emit empty EN when enPrimary is known.
  foreach ($id in @($result.Keys)) {
    $e = $result[$id]
    $enNames = Get-NameList $e.names.en
    if ($enNames.Count -gt 0) { continue }
    $fallback = [string]$e.enPrimary
    if ([string]::IsNullOrWhiteSpace($fallback)) { continue }
    $result[$id] = [pscustomobject]@{
      id         = $e.id
      score      = $e.score
      offParents = @($e.offParents)
      names      = [ordered]@{ en = (New-StringList @($fallback)); nl = (New-StringList $e.names.nl) }
      enPrimary  = $fallback
      nlPrimary  = $e.nlPrimary
    }
  }

  Write-Host "Uniqueness: merged $($mergeLog.Count) duplicate-primary entries; dropped $($dropLog.Count) colliding aliases."
  foreach ($line in ($mergeLog | Select-Object -First 15)) { Write-Host "  $line" }
  foreach ($line in ($dropLog | Select-Object -First 15)) { Write-Host "  $line" }

  return ,@($result.Values)
}

$resolved = Resolve-UniqueCatalog @($selected.Values)
$final = @($resolved | Sort-Object { Get-Norm (Get-NameList $_.names.nl)[0] }, id)
Write-Host "Final curated count: $($final.Count)"

$langCounts = @{}
foreach ($line in $lines) {
  if ($line -match '^([a-z]{2}(?:_[a-z]{2})?):') {
    $k = $Matches[1]
    if (-not $langCounts.ContainsKey($k)) { $langCounts[$k] = 0 }
    $langCounts[$k]++
  }
}
$availableKeys = @($langCounts.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { $_.Key })

function Escape-JsonString([string]$value) {
  if ($null -eq $value) { return '""' }
  $sb = New-Object Text.StringBuilder
  [void]$sb.Append('"')
  foreach ($ch in $value.ToCharArray()) {
    switch ($ch) {
      '"' { [void]$sb.Append('\"') }
      '\' { [void]$sb.Append('\\') }
      "`n" { [void]$sb.Append('\n') }
      "`r" { [void]$sb.Append('\r') }
      "`t" { [void]$sb.Append('\t') }
      default {
        if ([int][char]$ch -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', [int][char]$ch) }
        else { [void]$sb.Append($ch) }
      }
    }
  }
  [void]$sb.Append('"')
  return $sb.ToString()
}

function Write-JsonStringArray([Text.StringBuilder]$sb, [object]$values, [string]$indent) {
  $items = Get-NameList $values
  if ($items.Count -eq 0) {
    [void]$sb.Append('[]')
    return
  }
  [void]$sb.AppendLine('[')
  for ($i = 0; $i -lt $items.Count; $i++) {
    [void]$sb.Append($indent)
    [void]$sb.Append('  ')
    [void]$sb.Append((Escape-JsonString $items[$i]))
    if ($i -lt $items.Count - 1) { [void]$sb.Append(',') }
    [void]$sb.AppendLine()
  }
  [void]$sb.Append($indent)
  [void]$sb.Append(']')
}

$jsonPath = Join-Path $outDir 'curated-ingredients.json'
$json = New-Object Text.StringBuilder
[void]$json.AppendLine('{')
[void]$json.AppendLine('  "schemaVersion": 1,')
[void]$json.AppendLine('  "title": ' + (Escape-JsonString 'Curated recipe ingredient catalog') + ',')
[void]$json.AppendLine('  "description": ' + (Escape-JsonString 'Bootstrap catalog for RecipeLibrary CanonicalIngredient matching: culinary names only (not full OFF dump). Dutch is the primary UI catalog language; English is stored for bilingual seed data and future UI. See docs/ingredient-catalog.md.') + ',')
[void]$json.AppendLine('  "documentation": "docs/ingredient-catalog.md",')
[void]$json.AppendLine('  "source": {')
[void]$json.AppendLine('    "name": ' + (Escape-JsonString 'Open Food Facts ingredients taxonomy (primary) + manual Dutch kitchen staples') + ',')
[void]$json.AppendLine('    "url": "https://github.com/openfoodfacts/openfoodfacts-server/blob/main/taxonomies/food/ingredients.txt",')
[void]$json.AppendLine('    "licenseNote": ' + (Escape-JsonString 'OFF data is contributed under Open Food Facts terms (generally ODbL for the database). Review upstream terms before redistributing derived datasets.') + ',')
[void]$json.AppendLine('    "generatedBy": "scripts/generate-curated-ingredients.ps1"')
[void]$json.AppendLine('  },')
[void]$json.AppendLine('  "languageKeys": {')
[void]$json.AppendLine('    "description": ' + (Escape-JsonString 'Keys match OFF taxonomy line prefixes: "<key>: primary, synonym, ...". First array entry is preferred display name; further entries are aliases.') + ',')
[void]$json.Append('    "included": ')
Write-JsonStringArray $json @('en', 'nl') '    '
[void]$json.AppendLine(',')
[void]$json.AppendLine('    "howToExtend": ' + (Escape-JsonString 'Locate the ingredient in ingredients.txt via English primary name / id, then add e.g. names.fr from the fr: line. Regional variants use underscore (pt_br, zh_cn). xx is language-independent. Full rationale: docs/ingredient-catalog.md.') + ',')
[void]$json.Append('    "availableInSource": ')
Write-JsonStringArray $json $availableKeys '    '
[void]$json.AppendLine()
[void]$json.AppendLine('  },')
[void]$json.AppendLine('  "count": ' + $final.Count + ',')
[void]$json.AppendLine('  "ingredients": [')
for ($i = 0; $i -lt $final.Count; $i++) {
  $e = $final[$i]
  [void]$json.AppendLine('    {')
  [void]$json.AppendLine('      "id": ' + (Escape-JsonString ([string]$e.id)) + ',')
  [void]$json.AppendLine('      "names": {')
  [void]$json.Append('        "en": ')
  Write-JsonStringArray $json $e.names.en '        '
  [void]$json.AppendLine(',')
  [void]$json.Append('        "nl": ')
  Write-JsonStringArray $json $e.names.nl '        '
  [void]$json.AppendLine()
  [void]$json.AppendLine('      },')
  [void]$json.Append('      "offParents": ')
  Write-JsonStringArray $json $e.offParents '      '
  [void]$json.AppendLine()
  [void]$json.Append('    }')
  if ($i -lt $final.Count - 1) { [void]$json.Append(',') }
  [void]$json.AppendLine()
}
[void]$json.AppendLine('  ]')
[void]$json.AppendLine('}')
[IO.File]::WriteAllText($jsonPath, $json.ToString(), [Text.UTF8Encoding]::new($false))

$csv = New-Object Text.StringBuilder
[void]$csv.AppendLine('id,en,nl,en_aliases,nl_aliases')
foreach ($e in $final) {
  $enNames = Get-NameList $e.names.en
  $nlNames = Get-NameList $e.names.nl
  $enA = ($enNames | Select-Object -Skip 1) -join '|'
  $nlA = ($nlNames | Select-Object -Skip 1) -join '|'
  $en0 = if ($enNames.Count -gt 0) { $enNames[0] } else { '' }
  $nl0 = if ($nlNames.Count -gt 0) { $nlNames[0] } else { '' }
  $row = '"{0}","{1}","{2}","{3}","{4}"' -f `
    $e.id, `
  ($en0 -replace '"', '""'), `
  ($nl0 -replace '"', '""'), `
  ($enA -replace '"', '""'), `
  ($nlA -replace '"', '""')
  [void]$csv.AppendLine($row)
}
[IO.File]::WriteAllText((Join-Path $outDir 'curated-ingredients.csv'), $csv.ToString(), [Text.UTF8Encoding]::new($false))

Write-Host "Wrote $jsonPath ($((Get-Item $jsonPath).Length) bytes)"
Write-Host "Docs: docs/ingredient-catalog.md (README under data/ingredients/ is not overwritten)."
Write-Host "Spot checks:"
foreach ($id in @('tomato', 'onion', 'beef', 'pork', 'minced-meat', 'gelatine', 'saffron', 'andijvie', 'endive', 'ketjap-manis', 'courgette', 'clementine', 'brown-sugar', 'avocado', 'pasta', 'spruitjes', 'havermout', 'oyster-mushroom', 'red-cabbage')) {
  $hit = $final | Where-Object { $_.id -eq $id -or ((Get-NameList $_.names.nl) -contains $id) } | Select-Object -First 1
  if (-not $hit -and $id -eq 'spruitjes') { $hit = $final | Where-Object { $_.id -eq 'brussels-sprout' } | Select-Object -First 1 }
  if (-not $hit -and $id -eq 'havermout') { $hit = $final | Where-Object { $_.id -eq 'oats' } | Select-Object -First 1 }
  if (-not $hit -and $id -eq 'oyster-mushroom') { $hit = $final | Where-Object { $_.id -eq 'oyster-mushroom' } | Select-Object -First 1 }
  if (-not $hit -and $id -eq 'red-cabbage') { $hit = $final | Where-Object { $_.id -eq 'red-cabbage' } | Select-Object -First 1 }
  if ($hit) {
    $enNames = Get-NameList $hit.names.en
    $nlNames = Get-NameList $hit.names.nl
    $en0 = if ($enNames.Count -gt 0) { $enNames[0] } else { '' }
    $nl0 = if ($nlNames.Count -gt 0) { $nlNames[0] } else { '' }
    Write-Host ("  OK {0}: {1} / {2}" -f $id, $en0, $nl0)
  }
  else { Write-Host "  MISSING $id" }
}
