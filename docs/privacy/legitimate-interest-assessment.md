# Belangenafweging RecipeLibrary

Besluitdatum: 19 november 2026. Verantwoordelijke: Vincent Advocaat, VA Software Engineering. Contact: vincent@vasoftware.nl. Deze vastlegging betreft de huidige beperkte bezoekersmetingen en technische beschikbaarheids-/beveiligingsregistratie. Zij is geen verklaring dat iedere toekomstige toepassing automatisch rechtmatig is.

## Besluit en afbakening

Voor de hieronder afgebakende verwerking gebruiken we artikel 6 lid 1 onder f AVG. Account- en inhoudsverwerking voor een aangevraagde accountdienst heeft afzonderlijk de contractuele grondslag. De uitkomst geldt uitsluitend zolang de beschreven beperking en bezwaarmogelijkheden blijven bestaan.

De website gebruikt geen advertentie- of analyticscookies, geen fingerprinting en geen koppeling van verkeersrecords aan accounts. Een verzoeknummer ondersteunt een technische gebeurtenisketen, geen bezoekersprofiel. De eerdere dagelijkse HMAC van IP-adressen is geschrapt: dat identificatiemiddel was voor de gestelde vragen niet noodzakelijk. Ook bestaande waarden worden vóór afronding van de wijziging uit de actieve opslag verwijderd. Tot dat onderhoud klaar is redigeert het rapport legacywaarden al naar leeg; de beheerder moet de opschoonuitkomst verifiëren.

## 1. Concreet gerechtvaardigd belang

Het belang is een bereikbare, veilige recepten- en demonstratiedienst met beheersbaar verbruik. De eigenaar moet kunnen verklaren welke pagina's en verwijzende domeinen verkeer veroorzaken en welke appactie een databaseverbinding of mislukte start uitlokt. De gratis SQL-capaciteit was eerder uitgeput zonder bewust applicatiegebruik. Beschikbaarheid en bescherming tegen onbedoeld verbruik zijn echte operationele belangen, geen algemene wens om zoveel mogelijk bezoekersinformatie te verzamelen.

Technische SQL-gebeurtenissen bevatten tijd, vaste categorie, pad en verzoek-/pogingnummer. Optionele bezoekersmetingen voegen HTTP-status, methode, een grof browser-/botlabel en het verwijzende domein toe. Bronindicaties worden niet gebruikt om een recruiter, sollicitant of andere natuurlijke persoon te beoordelen. Een bezoek vanaf een vacaturesite kan beroepsmatige belangstelling suggereren; juist daarom zoeken we geen individuele identiteit, bewaren we geen zoektermen of bronpagina's en trekken we geen persoonsgerichte conclusies.

## 2. Noodzaak en minder ingrijpende mogelijkheden

| Mogelijkheid | Afweging |
| --- | --- |
| Alleen Azure-totaalmeters | Tonen belasting, maar geen verband tussen een appverzoek en SQL-start of verwijzend domein. Ze blijven een aanvullende bron voor platformactiviteiten. |
| Alleen geaggregeerde aantallen | Onvoldoende voor een concrete foutketen. Daarom beperkte details en verzoeknummers voor maximaal 31 dagen, geen onbeperkte sessiegeschiedenis. |
| Dagelijkse IP-pseudoniemen | Niet nodig voor bronnen, paden of de technische keten. Verwijderd; de applicatie leest hiervoor RemoteIpAddress niet meer. |
| Externe advertentie-/analyticsdienst | Levert meer verwerking, leveranciers en mogelijk kosten op. Niet gekozen. |
| Alleen serverregistratie | Mist herkomst op de onafhankelijke statische welkomsite. Een beperkte cookieloze browsermelding bevat daarom alleen gebeurtenistype, pad en verwijzende origin. |
| Geen registratie | Zou storingsonderzoek en bescherming van de beperkte capaciteit onvoldoende mogelijk maken. |

Ruwe IP-adressen, volledige user-agents, querystrings, volledige referer-URL's, inhoud van formulieren, account-id's en cookiewaarden worden niet in analytics opgeslagen. De browser verkort de verwijzende URL vóór verzending; de server sanitiseert opnieuw. De netwerklevering zelf blijft IP-verwerking door de infrastructuur vergen. De afzonderlijke demo-misbruiklimiet gebruikt een IP-hash voor de aangevraagde demodienst en valt niet onder deze bezoekersanalyse.

## 3. Belangen en gevolgen voor bezoekers

Een bezoeker mag beperkte operationele registratie verwachten. Een langdurig profiel of overdracht aan advertentieplatforms zou die verwachting overschrijden. De beperkte labels en het ontbreken van accountkoppeling verlagen het risico, maar verzoeknummers, tijd en paden behandelen we uit voorzorg nog steeds als potentieel persoonsgegevens. We noemen de verwerking niet volledig anoniem.

Er is geen verspreiding van verkeer naar Google Analytics of advertentiepartijen, geen hergebruik voor marketingprofielen, geen tracking over websites en geen besluit met juridische of vergelijkbaar ingrijpende gevolgen. De website is niet specifiek op kinderen gericht. Ook bij toevallig bezoek door kinderen maken we geen profielen; opschaling of kindgerichte functies vergen een nieuwe afweging.

## Waarborgen en bezwaar

- Optionele browser- en serverbezoekersmetingen stoppen bij DNT:1, Sec-GPC:1 of de hostgebonden functionele voorkeur RecipeLibrary.Analytics=off. De privacy-pagina is op welkomsite en app beschikbaar zonder SQL en legt de voorkeur per host uit. Wissen van de voorkeur heft een browserbrede DNT/GPC-keuze niet op.
- Noodzakelijke SQL-/foutdiagnostiek blijft werken met verzoeknummers en vaste categorieën, zonder IP, browser of referer. Dit beperkte onderzoek is nodig om de dienst te beveiligen en beschikbaar te houden; de uitschakelknop beschrijft deze uitzondering uitdrukkelijk.
- Gegevens zijn alleen bereikbaar achter een aparte beheersleutel; TLS, managed identity, beperkte Cosmos-rechten en bestaande kostenbewaking blijven intact. Rapporten worden niet publiek gedeeld.
- Actieve beschikbaarheid maximaal 31 dagen. De applicatie berekent item-TTL opnieuw naar de vaste vervaldatum van het UTC-uur; een update verlengt de kalenderbewaring niet. Dit is met de bestaande retention-regressietest gedekt. Alleen de defaultTtl-instelling zou daarvoor niet voldoende zijn. Periodieke Cosmos-backups: maximaal acht uur extra versiebehoud.
- Per uur maximaal 200 details, beperkte dimensies en wachtrij, met zichtbare uitval/steekproef. Geen onbegrensde persoonsgeschiedenis.
- Verzoeken om inzage, verwijdering of bezwaar kunnen naar het zakelijke contactadres. Er worden geen aanvullende identifiers verzameld uitsluitend om bezoekers later te identificeren. De afhandeling volgt het operationele draaiboek.

## Cookie-/terminalregel en grondslag zijn verschillende toetsen

De uitzondering voor functionele en beperkte, weinig ingrijpende analyse in artikel 11.7a Telecommunicatiewet is afzonderlijk van de AVG-grondslag beoordeeld. Cookieloos betekent niet automatisch buiten die regel: de browsermelding leest ook een paginapad en verwijzende URL. De huidige meting dient uitsluitend de kwaliteit/effectiviteit en beveiliging van de eigen dienst, zonder unieke bezoekerscode, externe analyseleverancier of persoonsprofiel. Met de beschreven waarborgen beoordelen we de impact als gering en kiezen we geen toestemmingsbanner. De gebruiker wordt wel geïnformeerd en kan optionele metingen uitschakelen.

## DPIA-screening en herbeoordeling

De huidige beperkte verwerking kent geen grootschalige stelselmatige persoonsmonitoring, verwerking van bijzondere gegevens als doel, accountprofilering of ingrijpende automatische besluiten. Voor deze bezoekersmetingen zien we daarom geen aanwijzing voor een verplichte DPIA. Dit is een screening van deze functionaliteit, geen organisatiebrede vrijstelling.

Herbeoordeel vóór toevoeging van bezoekers-id's, fingerprinting, accountkoppeling, advertentie-/retargetingpixels, persoonsgerichte campagnelinks, andere leveranciers, langere bewaring, nieuwe AI-doeleinden, kindgerichte functies of gevoelige gegevens. Doe daarnaast jaarlijks en na een incident een controle op deze afweging en het werkelijke netwerk-/cookiegedrag. Substantiële wijzigingen vragen opnieuw code-review en verificatie; een tekstwijziging alleen is onvoldoende.

## Bronnen

- AP, grondslag en drie voorwaarden: https://autoriteitpersoonsgegevens.nl/themas/basis-avg/avg-algemeen/grondslagen-avg-uitgelegd
- AP, cookies en beperkte analytics: https://autoriteitpersoonsgegevens.nl/nl/onderwerpen/internet-telefoon-tv-en-post/cookies
- AP, recht op informatie: https://autoriteitpersoonsgegevens.nl/nl/zelf-doen/gebruik-uw-privacyrechten/recht-op-informatie
- AVG artikelen 5, 6, 11, 12, 13, 21, 28 en 35: https://eur-lex.europa.eu/eli/reg/2016/679/oj
- Microsoft-contract- en configuratiebewijs: microsoft-assessment.md.
