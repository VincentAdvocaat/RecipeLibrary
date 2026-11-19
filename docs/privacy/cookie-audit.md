# Cookiecontrole en verwerkingsregister

Controledatum: 19 november 2026. Geen betaalde scanner of CMP gebruikt. De openbare informatie staat op /privacy.html op beide hosts; /analytics-privacy.html blijft een alias. Deze inventaris scheidt geobserveerd gedrag van broncode-/configuratiebewijs.

## Live nulmeting vóór de wijziging

Een nieuwe Chromium-context bezocht de welkomsite en de meetinformatie, en vroeg alleen GET /api/system/start-token op. Geen login, database-start of SQL-query uitgevoerd. Cookie- en opslagwaarden zijn niet vastgelegd.

- Alleen de eigen welkomhost en applicatiehost zijn in de browserrequests gezien.
- acaAffinity: applicatiehost, pad /, Secure, HttpOnly, SameSite=None, sessiecookie. Noodzakelijke Azure-routering voor de interactieve app, geen analytics-id.
- .AspNetCore.Antiforgery.*: applicatiehost, pad /, HttpOnly, SameSite=Strict, sessiecookie. De nulmeting miste Secure. Deze wijziging configureert SameAsRequest expliciet zodat HTTPS ook Secure oplevert; HTTP-localhost blijft testbaar.
- Geen localStorage- of sessionStorage-sleutels bij deze anonieme route.
- Dit bewijst niet de afwezigheid van functionele cookies na login, demo, taalkeuze of andere handelingen. Die zijn afzonderlijk uit broncode geïnventariseerd in de gepubliceerde tabel.

## Functionele opslag uit code

Identity.Application heeft standaard 14 dagen geldigheid met sliding expiration; Login kiest persistent=true. Demo overschrijft de absolute expiratie met de demosessie. Antiforgery is sessiegebonden. Taal, maateenheid en ShoppingListGroupId hebben een jaar; Demo-marker tot sessie-einde plus één dag. DemoHint in sessionStorage voorkomt herhaling van een eigen demomelding; de dashboardsleutel wordt alleen voor de expliciet ingelogde beheerder in het eigen tabblad bewaard en bij afmelden gewist.

De nieuwe RecipeLibrary.Analytics=off cookie bevat uitsluitend de expliciet gevraagde uitschakelvoorkeur. Host-only, pad /, SameSite=Lax, Secure bij HTTPS, maximaal één jaar. Deze cookie is bewust leesbaar door het eigen script om meldingen op de statische site te stoppen; er staat geen identiteit of geheim in. De app leest dezelfde voorkeur aan de serverzijde. De voorkeur wordt niet over andere vasoftware.nl-hosts verspreid. DNT/GPC hebben voorrang. Noodzakelijke aanmeldcookies blijven HttpOnly en Secure op HTTPS.

Een acceptatiebanner is niet gekozen voor deze inrichting. Er zijn geen advertentie-/trackingcookies of unieke bezoekerscodes. De juridische afweging staat in legitimate-interest-assessment.md. Bij een toekomstige tracker is opnieuw beoordelen en zo nodig vooraf toestemming verzamelen verplicht; een functionele-cookieverklaring dekt dat niet automatisch.

## Externe netwerkstromen

Dynamische afbeeldingen laden alleen automatisch van bekende eigen paden of de exact geconfigureerde eigen AzureBlobhost. Andere HTTPS-bronnen zijn expliciete noreferrer-links, geen automatische requests; onveilige schema's/hostconstructies worden geweigerd. Eigen demo-afbeeldingen en uploads blijven werken. Er is geen betaalde imageproxy of cache toegevoegd.

Externe AI stond bij de audit aan. De wijziging zet enableExternalAi standaard false in Bicep en de UI-fallback standaard uit. Gewone imports en lokale OCR blijven bestaan. Zonder aparte verwerkers-/doorgiftebeoordeling en kostenbesluit wordt directe OpenAI-verwerking niet opnieuw aangezet. De Microsoft-DPA dekt dit niet.

Een expliciet aangevraagde publieke import kan de bronwebsite en voor YouTube de bestaande Google-diensten bereiken vanaf de server. Er worden geen browsercookies, bezoekers-id of appaccount naar die diensten doorgestuurd. De gebruiker moet geen persoonlijke/geheime URL's importeren. Beschikbaarheid of geldigheid van bestaande API-sleutels is geen bewijs van een afzonderlijk providercontract.

## Beknopt register

| Verwerking | Doel en grondslag | Gegevens/ontvangers | Bewaring |
| --- | --- | --- | --- |
| Hosting en levering | Gevraagde dienst; overeenkomst waar van toepassing, verder gerechtvaardigd belang | Netwerk/IP en technische metadata, Microsoft Azure | Dienstspecifieke DPA/platformtermijnen; eigen beheerlog Activity Log standaard 90 dagen |
| Account en inhoud | Gevraagde account-/receptendienst, art.6(1)b | Gebruikersnaam, e-mail, wachtwoordhash, tokens, eigen inhoud en foto's; Microsoft | Zolang account/dienst bestaat tot verwerkte verwijdering; SQL korte backups 7 dagen |
| Beperkte analytics | Beschikbaarheid, bronnen en gebruik eigen dienst, art.6(1)f | Tijd/pad/origin/coarse labels/status/verzoeknummer, alleen beheerder en Microsoft als hostingverwerker | Vaste kalendertermijn maximaal 31 dagen; Cosmos-versies 8 uur extra |
| SQL-diagnostiek | Beveiliging, storingen en kostenbeheersing, art.6(1)f | Vaste categorie, tijd, gesanitiseerd pad, verzoek-/pogingnummer, Microsoft/beheerder | Zelfde begrensde analyticsopslag |
| Demo-misbruikbeperking | Tijdelijke demo en bescherming, overeenkomst/gerechtvaardigd belang | Tijdelijke accountinhoud en afzonderlijke IP-hash/quota, Microsoft | Toegang 60 min; fysieke cleanup eerstvolgende actieve ronde, met mogelijke pauzevertraging; marker+1dag |
| Functionele voorkeuren | Gevraagde voorkeur, gerechtvaardigd belang | Alleen genoemde cookies/tabbladopslag in eigen browser | Tabel op privacy.html |
| Privacyrechten | AVG-verplichting, art.6(1)c | Minimale correspondentie/accountverificatie, beheerder en bestaande zakelijke e-mailvoorziening | Afhandeling plus maximaal 1 jaar behoudens concreet geschil/wet |

De zakelijke contactidentiteit is overgenomen uit de bestaande zakelijke afzender van de beheerder; er is geen nieuw of onbevestigd privacymailadres aangemaakt. Voorwaarden van de zakelijke mailvoorziening zijn geen onderdeel van de Azure-MCA-controle. Er is geen testmail of ander bericht verstuurd.

## Grenzen en vrijgavecontrole

Cookievrij is geen synoniem voor volledig anoniem. Ook zonder IP-pseudoniem blijven verzoeknummers/tijden mogelijk persoonsgegevens. Demoquotalimieten zijn een aparte verwerking. Geen belofte van uitsluitende EU-verwerking, perfecte anonimiteit of onmiddellijke fysieke verwijdering tijdens SQL-pauze. De privacytekst maakt die grenzen zichtbaar.

Voor vrijgave: relevante backend-/browser-/componenttests, publieke statische privacyroute zonder SQL, functionele uitschakelcookie op beide hosts, DNT/GPC, legacyrapportredactie, echte API401 en beheerrapport200, cookie-Secure-attribuut, externe AI uit en eenmalige opschoning van oude Visitor-velden. De concrete post-deployment resultaten worden bij de PR vastgelegd; de nulmeting hierboven mag daarvoor niet als vervanging gelden.
