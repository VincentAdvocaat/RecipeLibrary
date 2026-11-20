# Microsoft-afspraken en Azure-inrichting

Onderzoeksdatum: 19 november 2026. Dit document onderbouwt de privacyverklaring van RecipeLibrary. Het is een momentopname van alleen-lezen controles, aangevuld met publieke Microsoft-voorwaarden. Er zijn geen voorwaarden geaccepteerd, gegevens naar leveranciers verzonden, diensten aangezet of rechten gewijzigd. Sleutels, facturen, klantadressen en loginhoud zijn niet opgevraagd.

## Conclusie

De subscriptionmetadata bevestigt een Microsoft Customer Agreement. De actuele publieke DPA en de MCA Product Terms geven het relevante Microsoft-kader. Een afzonderlijke ondertekende verwerkersovereenkomst, acceptatiedatum, contractspartij op de factuur of eventuele individuele afwijkingen zijn niet vastgesteld. Schrijf daarom niet dat alle contractstukken afzonderlijk zijn gecontroleerd of opnieuw ondertekend.

De regionale applicatie- en analyticsresources staan in Sweden Central. Dit rechtvaardigt geen algemene belofte dat alle verwerking uitsluitend in de EU plaatsvindt. De statische welkomsite wordt wereldwijd gedistribueerd; ook Microsoft documenteert uitzonderingen voor ondersteuning, beveiliging en netwerkverkeer. De ingestelde bewaartermijn van analytics is geen bewaartermijn voor alle Microsoft-gegevens of voor accountgegevens in SQL.

## Accountgebonden gecontroleerd

De controles betreffen de bestaande RecipeLibrary-subscription, eindigend op `2215`, en resourcegroep `rg-recipelibrary-test-sec`. De NEU-resourcegroep gaf een lege resourcelijst terug. Andere projecten binnen dezelfde subscription zijn niet inhoudelijk onderzocht.

| Onderdeel | Live bevinding | Betekenis en grens |
| --- | --- | --- |
| Overeenkomsttype | Billing Property API, versie 2024-04-01: `billingAccountAgreementType=MicrosoftCustomerAgreement` | Accountgebonden bewijs voor MCA. De brede billing-accountlijst mislukte met Partial Content; de gerichte subscriptionquery slaagde. |
| Regionale resources | Container App, managed environment, SQL-server/database, storageaccount, Key Vault en Cosmos-account: Sweden Central | Bevestigt de gerapporteerde resourcelocaties, geen garantie over ieder ondersteunend proces of back-up van andere diensten. |
| Statische welkomsite | `swa-recipelibrary-test-sec`, West Europe, Free | Administratieve locatie. De statische distributie is wereldwijd volgens Microsoft. |
| Analyticsopslag | `cosmos-analytics-txv3wjhyrukva`: alleen Sweden Central, geen zoneredundantie; lokale authenticatie uit | De controle heeft geen documentinhoud gelezen en geen toegangsrechten gewijzigd. |
| Analytics-TTL | `analytics/traffic`: `defaultTtl=2678400`, partitiesleutel `/day` | Standaard 31 dagen vanaf de laatste wijziging van een item. Dit is op zichzelf geen bewijs dat ieder item een vaste kalendertermijn heeft: iteminstellingen en applicatiegedrag blijven relevant. |
| Analyticsback-up | Periodic, interval 240 minuten, retentie 8 uur, redundantie Local | Back-ups kunnen verwijderde gegevens nog tijdelijk bevatten. Herstel is niet getest. |
| Analyticskosten | `enableFreeTier=true`, accountplafond 1.000 RU/s | Geen nieuwe betaalde voorzieningen toegevoegd. Opslagbegrenzing door de applicatie blijft nodig; dit onderzoek is geen kostenmeting. |
| Container Apps-logging | `appLogsConfiguration.destination=null`, `logAnalyticsConfiguration=null` | Geen Log Analytics-bestemming ingesteld. Geen afzonderlijke persistente consolelogretentie kunnen vaststellen. |
| Log Analytics en Application Insights | Subscriptionbrede ARM-resourcelijsten voor beide typen: leeg | Geen zichtbare resources van die typen. Dit betekent niet dat er geen Microsoft-platformlogs of live logstreams bestaan. |
| Diagnostic settings | Leeg op Container App, environment, Cosmos-account, Static Web App, Key Vault, storageaccount, blobservice en applicatie-SQL-database | Geen gevonden resource-export naar workspace, Event Hub of storage op deze acht gecontroleerde scopes. Geen volledige audit van alle resources en subresources. |
| Subscriptiondiagnostics | `az monitor diagnostic-settings subscription list`: `value=[]` | Geen subscriptionexport aangetroffen. Azure Activity Log blijft een afzonderlijk platformonderdeel. |
| SQL-back-ups | Applicatiedatabase: korte retentie 7 dagen, differentieel interval 12 uur | Deze termijn geldt niet voor analytics in Cosmos. Langetermijnback-ups en historische verwijderde resources zijn niet onderzocht. |
| Blobopslag | Blob soft delete uit; container soft delete en versioning niet ingevuld | Geen ingestelde aanvullende termijn aangetroffen in deze eigenschappen. Geen levenscyclusregels of individuele blobs onderzocht. |
| Key Vault | Soft-delete-retentie 90 dagen | Betreft verwijderde vaultobjecten; niet de bewaartermijn van bezoekersstatistieken. |

De CLI-controles gebruikten uitsluitend managementmetadata. Er zijn geen requests naar de publieke applicatie gedaan en geen SQL-query's of analytics-events aangemaakt.

## Publieke Microsoft-voorwaarden

### DPA en MCA

Microsoft publiceert als actuele DPA de editie van **1 november 2026**. De Engelse DOCX is gedownload van de officiële site en de relevante tekst is gelezen. Het bestand had SHA-256 `f324f04afcb9763324411da705e6df6d79b704c74dab04f0a6e62bc2404425aa`.

De DPA beschrijft verwerking voor dienstverlening en bijbehorende bedrijfsvoering. Microsoft is doorgaans verwerker of subverwerker, maar kan voor de beschreven bedrijfsvoering zelfstandig verwerkingsverantwoordelijke zijn. De DPA bevat afspraken over beveiliging, incidenten, ondersteuning bij betrokkenenrechten, subverwerkers en doorgiften. Dat ondersteunt de leveranciersbeoordeling, maar vervangt onze eigen grondslag en informatieplicht niet.

De algemene beëindigingsregeling noemt, behoudens uitzonderingen waaronder gratis trials, 90 dagen beperkte toegang en vervolgens verwijdering binnen nog eens 90 dagen. Deze regeling is geen operationele analyticsretentie en geen belofte van onmiddellijke verwijdering uit alle systemen. De DPA verwijst voor internationale doorgiften naar de door Microsoft geïmplementeerde SCC's uit 2021 en aanvullende waarborgen; de definitie betreft de processor-naar-processorafspraken tussen Microsoft Ireland Operations Limited en Microsoft Corporation.

Bronnen: [DPA-publicatie en archief](https://www.microsoft.com/licensing/docs/view/Microsoft-Products-and-Services-Data-Protection-Addendum-DPA), [Engelse DPA november 2026](https://www.microsoft.com/licensing/docs/documents/download/MicrosoftProductandServicesDPA%28WW%29%28English%29%28November_2026%29%28CR%29.docx), [Azure Product Terms voor MCA](https://www.microsoft.com/licensing/terms/product/MicrosoftAzure/MCA), [Privacy & Security Terms voor MCA](https://www.microsoft.com/licensing/terms/product/PrivacyandSecurityTerms/MCA). De laatste bron neemt de DPA op en beschrijft de EU Data Boundary en uitzonderingen. Dit zijn publieke bepalingen, geen uitgelezen individuele contractacceptatie.

### EU Data Boundary en wereldwijde distributie

De EU Data Boundary kent voorwaarden voor regionale en niet-regionale diensten en omvat ook EFTA-landen. Microsoft beschrijft resterende doorgiften, zoals gecontroleerde toegang door medewerkers buiten het gebied, beveiliging, directorygegevens en netwerktransit. De gegevens blijven dus niet onder alle omstandigheden uitsluitend binnen de EU.

Voor de welkomsite is de [Static Web Apps FAQ](https://learn.microsoft.com/en-us/azure/static-web-apps/faq) specifiek: statische bestanden worden wereldwijd gedistribueerd. De instelling West Europe mag daarom niet als bewijs van uitsluitend Europese levering worden gebruikt. De [uitzonderingen voor Front Door en CDN](https://learn.microsoft.com/en-us/privacy/eudb/eu-data-boundary-excluded-services) verklaren de wereldwijde edgewerking van die diensten. Dit bewijst niet dat deze subscription een afzonderlijk Front Door- of CDN-profiel gebruikt, of welke edgeprovider een concrete bezoeker bediende.

Bronnen: [EU Data Boundary](https://learn.microsoft.com/en-us/privacy/eudb/eu-data-boundary-learn/), [blijvende grensoverschrijdende verwerkingen](https://learn.microsoft.com/en-us/privacy/eudb/eu-data-boundary-transfers-for-all-services), [configuratie niet-regionale Azure-diensten](https://learn.microsoft.com/en-us/privacy/eudb/eu-data-boundary-configure-azure-nonregional-services). De tenantinstelling voor de Resource Manager-boundary en de Entra-directorylocatie zijn niet vastgesteld. Deze inrichting mag dus niet als afzonderlijk gecertificeerd of volledig geconfigureerd worden beschreven.

### Subverwerkers en SCC's

De geraadpleegde officiële [Microsoft Online Services Subprocessors-lijst](https://api.servicetrust.microsoft.com/api/v2/downloadDocuments/115f1cb5-7f7b-4a5e-838d-df6d03c84dda) draagt uitgiftedatum 10 september 2026. Zij beschrijft categorieën leveranciers en verwerkingslocaties. Een vermelding betekent dat een leverancier voor de betrokken diensten kan worden ingezet, niet dat die aantoonbaar onze gegevens heeft verwerkt. Azure-klanten kunnen updates volgen via My Library in het [Service Trust Portal](https://servicetrust.microsoft.com/). Een notificatieabonnement is niet ingesteld of gecontroleerd. Er is geen accountgebonden overzicht van daadwerkelijk ingezette subverwerkers beschikbaar gemaakt in deze controle.

## Logging en bewaartermijnen juist formuleren

Microsoft beschrijft een standaardretentie van 90 dagen voor [Azure Activity Log](https://learn.microsoft.com/en-us/azure/azure-monitor/platform/activity-log). Dit is een publieke standaard voor beheeractiviteiten, geen live vastgestelde termijn voor alle logs. Resource-export en Log Analytics zijn afzonderlijke configuraties. [Container Apps-logbestemmingen](https://learn.microsoft.com/en-us/azure/container-apps/log-options) en [logstreams](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming) moeten bovendien worden onderscheiden van interne service- en beveiligingsverwerking door Microsoft.

Een verantwoorde tekst noemt voor bezoekersstatistieken de eigen ingestelde termijn, met tijdelijke aanwezigheid in back-ups. Schrijf niet dat Microsoft alle technische gegevens na 31 dagen verwijdert of geen IP-adressen kan verwerken. Een netwerkdienst moet verzoeken kunnen afhandelen; de applicatie kan tegelijk besluiten IP-adressen niet in haar eigen analytics vast te leggen. Dit onderzoek heeft de daadwerkelijke inhoud van providerlogs niet gecontroleerd.

## OpenAI en YouTube zijn afzonderlijke leveranciersstromen

Live staat `RecipeImport__Ai__Enabled=true`. De app verwijst voor de AI- en YouTube-sleutels naar respectievelijk `openai-api-key` en `youtube-api-key`. De sleutelwaarden, hun geldigheid en recente API-aanroepen zijn niet gecontroleerd. In de uitgelezen container-env staan geen AI-model- of endpointoverrides.

De broncode op basiscommit `ffbfb34` gebruikt standaard provider OpenAI, model `gpt-4o-mini` en `https://api.openai.com/v1/chat/completions`. De importcode verstuurt systeeminstructies en importinhoud. YouTube-import heeft een route via `www.googleapis.com/youtube/v3/videos` en een fallback via `www.youtube.com/youtubei/v1/player`. Dit is een broncodebevinding; andere configuratiebronnen en daadwerkelijk netwerkgebruik zijn niet uitputtend geverifieerd.

Dit betreft directe externe API's, geen aangetoonde Azure OpenAI-deployment. De Microsoft-DPA vormt geen bewijs voor een afzonderlijke OpenAI- of Google-overeenkomst, bewaartermijn, verwerkingslocatie of rechtsgrond. De privacyverklaring moet deze importfuncties afzonderlijk uitleggen als zij beschikbaar blijven. De eigen analyticsstroom mag niet worden voorgesteld als bewijs dat deze andere leveranciers ontbreken. Hun individuele overeenkomsten en eventuele bijzondere accountinstellingen blijven buiten deze Microsoft-controle.

Mitigatie in deze privacywijziging: externe AI-import wordt standaard uitgeschakeld via `enableExternalAi=false` en de UI vraagt AI-fallback niet meer standaard aan. Voor opnieuw inschakelen is een afzonderlijke leveranciers-/doorgiftebeoordeling en kostenbesluit nodig. Gewone import en lokale OCR blijven beschikbaar. Publieke bronimport blijft een door de gebruiker aangevraagde serverhandeling; browsercookies, bezoekerscode en accountgegevens worden daarbij niet doorgestuurd. Onbekende externe afbeeldingen worden expliciete links in plaats van automatische browserrequests. De uiteindelijke releasecontrole moet bevestigen dat de declaratieve AI-instelling ook live is toegepast.

De applicatie vult de standaard Cosmos-TTL aan met een per-item resterende termijn tot het vaste UTC-uur plus 31 dagen. De regressiontest `Merge_is_idempotent_bounded_and_retention_does_not_slide` controleert dit. Zo verlengen gewone updates de kalenderbewaring niet. Eenmalige legacy-redactie moet eveneens de oorspronkelijke vervaldatum aanhouden of verkorten.

## Wat dit dossier afrondt en wat het niet bewijst

Vastgelegd zijn het accountgebonden MCA-type, de actuele publieke DPA/SCC-verwijzingen en subverwerkerslijst, de geconfigureerde regio's, de aangetroffen loggingconfiguratie en de genoemde retentie-instellingen. Er hoeft voor deze vastlegging geen betaalde dienst, nieuw abonnement of nieuwe toestemming bij Microsoft te worden geactiveerd.

Niet bewezen zijn individuele acceptatiehistorie en afwijkende afspraken, feitelijke toegang door specifieke subverwerkers, volledige geografische routes, retentie van alle interne platformlogs, of contracten met OpenAI en Google. Presenteer die punten niet als gecontroleerd. Deze bevindingen kunnen worden verwerkt in de verklaring en belangenafweging; cookiegedrag en de eigen applicatiegrondslag worden door de integratiechat afzonderlijk beoordeeld.

## Wijziging 20 november 2026

Na expliciete opdracht is voor SEC Azure SQL Basic (5 DTU / 2 GiB) gekozen met overdag een warme appcontainer. De eerdere free-limit-waarnemingen hierboven zijn de nulmeting van 19 november, niet de nieuwe doelconfiguratie. Provider, regio, identiteiten en korte backupretentie blijven gelijk. Zie [uitvoeringsdraaiboek](../azure/daytime-basic.md); de live verificatie wordt bij de uitrol-PR vastgelegd.
