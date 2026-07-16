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

function Get-Norm([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return '' }
  $s = $s.Trim().ToLowerInvariant()
  $fd = $s.Normalize([Text.NormalizationForm]::FormD)
  $sb = New-Object Text.StringBuilder
  foreach ($ch in $fd.ToCharArray()) {
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
      [void]$sb.Append($ch)
    }
  }
  return ($sb.ToString().Normalize([Text.NormalizationForm]::FormC) -replace '[^a-z0-9\s]', ' ' -replace '\s+', ' ').Trim()
}

$denyName = [regex]::new(@'
^(e\d|e-\d)|\badditive\b|\bcolour\b|\bcolor\b|\bpreservative\b|\bemulsifier\b|\bstabiliser\b|\bstabilizer\b|\bthickener\b|\bsweetener\b|\bantioxidant\b|\bflavour\b|\bflavor enhancer\b|\bacidity regulator\b|\bfirming agent\b|\banti-caking\b|\bglazing agent\b|\braising agent\b|\bflour treatment\b|\bsequestrant\b|\bhumectant\b|\bpropellant\b|\bpackaging gas\b|\bmodified starch\b|\bglucose syrup\b|\bmaltodextrin\b|\blecithin\b|\bmono and diglyceride\b|\bpolyglycerol\b|\bcarrageenan\b|\bxanthan\b|\bguar gum\b|\blocust bean\b|\bcellulose\b|\bglycerol\b|\bsorbitol\b|\baspartame\b|\bsaccharin\b|\bcyclamate\b|\bacesulfame\b|\bsteviol\b|\benzyme\b|\brennet\b|\bculture\b|\bferment\b|\bstarter\b|\bbacteria\b|\bbifidus\b|\blactobacillus\b|\blysozyme\b|\bpectin\b|\bcaseinate\b|\bwhey powder\b|\bmilk protein\b|\bsoy protein isolate\b|\bhydrolysed\b|\bhydrogenated\b|\binteresterified\b|\bpalm fat\b|\bvegetable fat\b|\bfiber\b|\bfibre\b|\bextract\b|\boleoresin\b|\bconcentrate\b|\bisolate\b|\bhydrolysate\b|\bprotein powder\b|\bcollagen\b|\bingredient\b|\bpreparation\b|\bfilling\b|\btopping\b|\bcoating\b|\bcrumb\b|\bstarch\b|\bdextrin\b|\bglucose\b|\bfructose syrup\b|\binvert sugar\b|\bcaramelised\b|\bcaramelized\b
'@, 'IgnoreCase')

$denyParent = [regex]::new('additive|e\d{3}|colour|color|preservative|emulsifier|stabiliser|thickener|sweetener|antioxidant|flavouring|flavoring|processing aid', 'IgnoreCase')
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
$manual = @(
  @{ id = 'beef'; en = @('beef'); nl = @('rundvlees', 'rund') },
  @{ id = 'minced-meat'; en = @('minced meat', 'ground meat'); nl = @('gehakt') },
  @{ id = 'minced-beef'; en = @('minced beef', 'ground beef'); nl = @('rundergehakt') },
  @{ id = 'minced-pork'; en = @('minced pork'); nl = @('varkensgehakt') },
  @{ id = 'oats'; en = @('oats', 'oat'); nl = @('haver', 'havervlokken') },
  @{ id = 'paprika-spice'; en = @('paprika', 'ground paprika'); nl = @('paprikapoeder', 'paprika') },
  @{ id = 'saffron'; en = @('saffron'); nl = @('saffraan') },
  @{ id = 'tempeh'; en = @('tempeh'); nl = @('tempeh') },
  @{ id = 'baking-soda'; en = @('baking soda', 'sodium bicarbonate', 'bicarbonate of soda'); nl = @('natriumbicarbonaat', 'baking soda') },
  @{ id = 'gelatine'; en = @('gelatine', 'gelatin'); nl = @('gelatine') },
  @{ id = 'agar'; en = @('agar', 'agar-agar', 'agar agar'); nl = @('agar', 'agar-agar') },
  @{ id = 'courgette'; en = @('courgette', 'zucchini'); nl = @('courgette', 'zucchini') },
  @{ id = 'spring-onion'; en = @('spring onion', 'green onion', 'scallion'); nl = @('lente-ui', 'bosui') },
  @{ id = 'shallot'; en = @('shallot'); nl = @('sjalot') },
  @{ id = 'celeriac'; en = @('celeriac', 'celery root'); nl = @('knolselderij') },
  @{ id = 'swede'; en = @('swede', 'rutabaga'); nl = @('koolraap') },
  @{ id = 'endive'; en = @('Belgian endive', 'endive', 'chicory'); nl = @('witlof', 'witloof') },
  @{ id = 'andijvie'; en = @('curly endive', 'frisée'); nl = @('andijvie') },
  @{ id = 'lambs-lettuce'; en = @("lamb's lettuce", 'corn salad', 'mache'); nl = @('veldsla') },
  @{ id = 'rocket'; en = @('rocket', 'arugula', 'rucola'); nl = @('rucola') },
  @{ id = 'chinese-cabbage'; en = @('Chinese cabbage', 'napa cabbage'); nl = @('Chinese kool') },
  @{ id = 'bean-sprouts'; en = @('bean sprouts'); nl = @('tauge', 'taugé') },
  @{ id = 'stock-cube'; en = @('stock cube', 'bouillon cube'); nl = @('bouillonblokje') },
  @{ id = 'creme-fraiche'; en = @('creme fraiche', 'crème fraîche'); nl = @('crème fraîche', 'creme fraiche') },
  @{ id = 'smoked-salmon'; en = @('smoked salmon'); nl = @('gerookte zalm') },
  @{ id = 'icing-sugar'; en = @('icing sugar', 'powdered sugar'); nl = @('poedersuiker') },
  @{ id = 'brown-sugar'; en = @('brown sugar'); nl = @('bruine suiker', 'basterdsuiker') },
  @{ id = 'self-raising-flour'; en = @('self-raising flour', 'self rising flour'); nl = @('zelfrijzend bakmeel') },
  @{ id = 'cornflour'; en = @('cornflour', 'corn starch', 'cornstarch'); nl = @('maizena', 'maïzena', 'maiszetmeel') },
  @{ id = 'double-cream'; en = @('double cream', 'heavy cream', 'whipping cream'); nl = @('slagroom') },
  @{ id = 'margarine'; en = @('margarine'); nl = @('margarine') },
  @{ id = 'fish-sauce'; en = @('fish sauce'); nl = @('vissaus') },
  @{ id = 'oyster-sauce'; en = @('oyster sauce'); nl = @('oestersaus') },
  @{ id = 'worcestershire-sauce'; en = @('worcestershire sauce'); nl = @('worcestersaus') },
  @{ id = 'harissa'; en = @('harissa'); nl = @('harissa') },
  @{ id = 'pesto'; en = @('pesto'); nl = @('pesto') },
  @{ id = 'hummus'; en = @('hummus', 'houmous'); nl = @('hummus') },
  @{ id = 'phyllo'; en = @('phyllo', 'filo pastry'); nl = @('filodeeg') },
  @{ id = 'puff-pastry'; en = @('puff pastry'); nl = @('bladerdeeg') },
  @{ id = 'shortcrust-pastry'; en = @('shortcrust pastry'); nl = @('kruimeldeeg') },
  @{ id = 'tortilla'; en = @('tortilla'); nl = @('tortilla') },
  @{ id = 'pita'; en = @('pita', 'pitta'); nl = @('pita', 'pitabroodje') },
  @{ id = 'naan'; en = @('naan'); nl = @('naan') },
  @{ id = 'polenta'; en = @('polenta'); nl = @('polenta') },
  @{ id = 'bulgur'; en = @('bulgur', 'bulghur'); nl = @('bulgur') },
  @{ id = 'miso'; en = @('miso'); nl = @('miso') },
  @{ id = 'nori'; en = @('nori'); nl = @('nori') },
  @{ id = 'rice-vinegar'; en = @('rice vinegar'); nl = @('rijstazijn') },
  @{ id = 'apple-cider-vinegar'; en = @('apple cider vinegar', 'cider vinegar'); nl = @('appelazijn') },
  @{ id = 'mirin'; en = @('mirin'); nl = @('mirin') },
  @{ id = 'rice-noodles'; en = @('rice noodles'); nl = @('rijstnoedels') },
  @{ id = 'lemongrass'; en = @('lemongrass', 'lemon grass'); nl = @('citroengras', 'sereh') },
  @{ id = 'galangal'; en = @('galangal'); nl = @('galanga', 'laos') },
  @{ id = 'star-anise'; en = @('star anise'); nl = @('steranijs') },
  @{ id = 'fennel-seed'; en = @('fennel seed'); nl = @('venkelzaad') },
  @{ id = 'mustard-seed'; en = @('mustard seed'); nl = @('mosterdzaad') },
  @{ id = 'poppy-seed'; en = @('poppy seed'); nl = @('maanzaad') },
  @{ id = 'pumpkin-seed'; en = @('pumpkin seed'); nl = @('pompoenpit') },
  @{ id = 'pine-nut'; en = @('pine nut'); nl = @('pijnboompit') },
  @{ id = 'chestnut'; en = @('chestnut'); nl = @('kastanje') },
  @{ id = 'cranberry'; en = @('cranberry'); nl = @('cranberry', 'veenbes') },
  @{ id = 'pomegranate'; en = @('pomegranate'); nl = @('granaatappel') },
  @{ id = 'passion-fruit'; en = @('passion fruit'); nl = @('passievrucht') },
  @{ id = 'nectarine'; en = @('nectarine'); nl = @('nectarine') },
  @{ id = 'plum'; en = @('plum'); nl = @('pruim') },
  @{ id = 'grapefruit'; en = @('grapefruit'); nl = @('grapefruit') },
  @{ id = 'mandarin'; en = @('mandarin', 'tangerine', 'clementine'); nl = @('mandarijn', 'clementine') },
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
  @{ id = 'feta'; en = @('feta'); nl = @('feta') },
  @{ id = 'goat-cheese'; en = @('goat cheese'); nl = @('geitenkaas') },
  @{ id = 'ricotta'; en = @('ricotta'); nl = @('ricotta') },
  @{ id = 'mascarpone'; en = @('mascarpone'); nl = @('mascarpone') },
  @{ id = 'brie'; en = @('brie'); nl = @('brie') },
  @{ id = 'camembert'; en = @('camembert'); nl = @('camembert') },
  @{ id = 'gouda'; en = @('gouda'); nl = @('Goudse kaas', 'gouda') },
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
  @{ id = 'split-peas'; en = @('split peas'); nl = @('spliterwten') },
  @{ id = 'spek'; en = @('speck', 'smoked bacon'); nl = @('spek', 'rookspek') },
  @{ id = 'rookworst'; en = @('smoked sausage'); nl = @('rookworst') },
  @{ id = 'stroop'; en = @('syrup', 'treacle'); nl = @('stroop') },
  @{ id = 'apple-syrup'; en = @('apple syrup'); nl = @('appelstroop') },
  @{ id = 'pindakaas'; en = @('peanut butter'); nl = @('pindakaas') },
  @{ id = 'desiccated-coconut'; en = @('desiccated coconut', 'shredded coconut'); nl = @('kokosrasp') },
  @{ id = 'juniper-berry'; en = @('juniper berry'); nl = @('jeneverbes') },
  @{ id = 'mace'; en = @('mace'); nl = @('foelie') },
  @{ id = 'coriander-seed'; en = @('coriander seed'); nl = @('korianderzaad') },
  @{ id = 'fenugreek'; en = @('fenugreek'); nl = @('fenegriek') },
  @{ id = 'sumac'; en = @('sumac'); nl = @('sumak', 'sumac') },
  @{ id = 'tahini'; en = @('tahini', 'tahina'); nl = @('tahini', 'tahin') },
  @{ id = 'sambal'; en = @('sambal', 'sambal oelek'); nl = @('sambal', 'sambal oelek') },
  @{ id = 'ketjap-manis'; en = @('kecap manis', 'sweet soy sauce'); nl = @('ketjap', 'ketjap manis') },
  @{ id = 'shrimp-paste'; en = @('shrimp paste', 'trassi'); nl = @('trassi', 'terasi') },
  @{ id = 'chia-seed'; en = @('chia seed', 'chia seeds'); nl = @('chiazaad') },
  @{ id = 'linseed'; en = @('linseed', 'flax seed'); nl = @('lijnzaad') },
  @{ id = 'sunflower-seed'; en = @('sunflower seed'); nl = @('zonnebloempit') }
)

function Merge-Names([string[]]$existing, [string[]]$extra) {
  $list = New-Object System.Collections.Generic.List[string]
  $seen = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($x in @($existing + $extra)) {
    if ([string]::IsNullOrWhiteSpace($x)) { continue }
    $n = Get-Norm $x
    if ($seen.Add($n)) { [void]$list.Add($x.Trim()) }
  }
  return $list.ToArray()
}

foreach ($m in $manual) {
  if ($selected.ContainsKey($m.id)) {
    $ex = $selected[$m.id]
    $selected[$m.id] = [pscustomobject]@{
      id         = $m.id
      score      = [Math]::Max([int]$ex.score, 100)
      offParents = @($ex.offParents)
      names      = [ordered]@{
        en = @(Merge-Names $ex.names.en $m.en)
        nl = @(Merge-Names $ex.names.nl $m.nl)
      }
      enPrimary  = $m.en[0]
      nlPrimary  = $m.nl[0]
    }
  }
  else {
    $selected[$m.id] = [pscustomobject]@{
      id         = $m.id
      score      = 100
      offParents = @()
      names      = [ordered]@{ en = @($m.en); nl = @($m.nl) }
      enPrimary  = $m.en[0]
      nlPrimary  = $m.nl[0]
    }
  }
}

$final = @($selected.Values | Sort-Object { Get-Norm $_.names.nl[0] }, id)
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

$ingredientObjs = foreach ($e in $final) {
  [ordered]@{
    id         = $e.id
    names      = [ordered]@{
      en = @($e.names.en)
      nl = @($e.names.nl)
    }
    offParents = @($e.offParents)
  }
}

$doc = [ordered]@{
  schemaVersion = 1
  title         = 'Curated recipe ingredient catalog'
  description   = 'Bootstrap catalog for RecipeLibrary CanonicalIngredient matching: culinary names only (not full OFF dump). Dutch is the primary UI catalog language; English is stored for bilingual seed data and future UI. See docs/ingredient-catalog.md.'
  documentation = 'docs/ingredient-catalog.md'
  source        = [ordered]@{
    name        = 'Open Food Facts ingredients taxonomy (primary) + manual Dutch kitchen staples'
    url         = 'https://github.com/openfoodfacts/openfoodfacts-server/blob/main/taxonomies/food/ingredients.txt'
    licenseNote = 'OFF data is contributed under Open Food Facts terms (generally ODbL for the database). Review upstream terms before redistributing derived datasets.'
    generatedBy = 'scripts/generate-curated-ingredients.ps1'
  }
  languageKeys  = [ordered]@{
    description       = 'Keys match OFF taxonomy line prefixes: "<key>: primary, synonym, ...". First array entry is preferred display name; further entries are aliases.'
    included          = @('en', 'nl')
    howToExtend       = 'Locate the ingredient in ingredients.txt via English primary name / id, then add e.g. names.fr from the fr: line. Regional variants use underscore (pt_br, zh_cn). xx is language-independent. Full rationale: docs/ingredient-catalog.md'
    availableInSource = $availableKeys
  }
  count         = $ingredientObjs.Count
  ingredients   = @($ingredientObjs)
}

$jsonPath = Join-Path $outDir 'curated-ingredients.json'
[IO.File]::WriteAllText($jsonPath, ($doc | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

$csv = New-Object Text.StringBuilder
[void]$csv.AppendLine('id,en,nl,en_aliases,nl_aliases')
foreach ($e in $final) {
  $enA = (@($e.names.en) | Select-Object -Skip 1) -join '|'
  $nlA = (@($e.names.nl) | Select-Object -Skip 1) -join '|'
  $row = '"{0}","{1}","{2}","{3}","{4}"' -f `
    $e.id, `
  ($e.names.en[0] -replace '"', '""'), `
  ($e.names.nl[0] -replace '"', '""'), `
  ($enA -replace '"', '""'), `
  ($nlA -replace '"', '""')
  [void]$csv.AppendLine($row)
}
[IO.File]::WriteAllText((Join-Path $outDir 'curated-ingredients.csv'), $csv.ToString(), [Text.UTF8Encoding]::new($false))

Write-Host "Wrote $jsonPath ($((Get-Item $jsonPath).Length) bytes)"
Write-Host "Docs: docs/ingredient-catalog.md (README under data/ingredients/ is not overwritten)."
Write-Host "Spot checks:"
foreach ($id in @('tomato', 'onion', 'beef', 'minced-meat', 'gelatine', 'saffron', 'andijvie', 'ketjap-manis', 'courgette')) {
  $hit = $final | Where-Object { $_.id -eq $id } | Select-Object -First 1
  if ($hit) { Write-Host ("  OK {0}: {1} / {2}" -f $id, $hit.names.en[0], $hit.names.nl[0]) }
  else { Write-Host "  MISSING $id" }
}
