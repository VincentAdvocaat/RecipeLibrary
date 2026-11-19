# Afhandeling en onderhoud privacy

Eigenaar: Vincent Advocaat / VA Software Engineering, vincent@vasoftware.nl. Dit draaiboek hoort bij de publieke verklaring en de belangenafweging. Geen betaalde tooling, nieuwe abonnementen of nieuwe verwerkers nodig voor de huidige inrichting.

## Een verzoek ontvangen

1. Controleer de zakelijke inbox en leg ontvangstdatum, soort verzoek en uiterste antwoorddatum vast in een afgeschermd dossier, niet in een publiek GitHub-issue. Reageer normaal binnen één maand; gebruik alleen de wettelijke verlenging met tijdige redengeving.
2. Vraag uitsluitend noodzakelijke verduidelijking. Verifieer een account bij voorkeur via het bestaande e-mailadres. Vraag geen wachtwoord of standaard identiteitskopie. Verzamel geen nieuwe bezoekersidentifier om eerder niet-identificeerbare analytics alsnog te identificeren.
3. Bepaal de toepasselijke gegevens: account/inhoud in SQL, eigen foto's, token-/demogegevens, beperkte analytics en correspondentie. Maak onderscheid tussen een bezwaar tegen optionele statistiek en noodzakelijke beveiliging. Doe bij een concreet bezwaar opnieuw de belangenafweging; een algemene tekst is geen automatische afwijzing.
4. Exporteer of verwijder alleen het geverifieerde account en afhankelijke inhoud met een gecontroleerde, transactionele beheerhandeling. Controleer account-id en afhankelijkheden vóór mutaties; laat geen recepten, foto's of tokens van een ander account verwijderen. Laat een noodzakelijke uitvoeringswijziging eerst testen/reviewen. Er is geen nieuwe publieke beheer- of wis-API toegevoegd.
5. Een gepauzeerde gratis database kan uitvoering vertragen. Zet nooit betaald doordraaien aan voor de afhandeling. Informeer over die concrete vertraging en bewaak de wettelijke termijn; hervatte gratis capaciteit of bevoegde technische ondersteuning kan dan nodig zijn. Bewaar de reden en uitkomst.
6. Controleer het resultaat, beantwoord het verzoek via het geverifieerde kanaal en leg een minimale bevestiging van afhandeling vast. Verwijder onnodige kopieën. Afhandelingscorrespondentie maximaal één jaar bewaren, tenzij een concreet geschil/wet langer vereist.

## Back-ups en incidenten

Cosmos bewaart periodieke versies 8 uur; SQL korte backups 7 dagen. Herstel kan gewiste gegevens terugbrengen. Bewaar daarom in een afgeschermd verwijderregister alleen de minimale accountreferentie en afhandelingsdatum die nodig zijn om verwijdering na herstel opnieuw toe te passen; wis die referentie na de relevante backupperiode, tenzij een concrete verplichting langer vergt. Herstel is een afzonderlijke beheerhandeling, geen reden om nieuwe analytics-identificatie te introduceren.

Bij een mogelijk datalek: beperk toegang, stop de betreffende gegevensstroom waar nodig, bewaar minimaal onderzoeksmateriaal afgeschermd, beoordeel risico en toepasselijke melding aan AP binnen 72 uur en betrokkenen bij hoog risico. Neem Microsoft's DPA-procedure en ondersteuning mee. Voeg geen ruwe bezoekersgegevens of secrets toe aan openbare logs/issues. Een providerincident is niet automatisch bewijs dat niets hoeft te worden gemeld.

## Controle na deze release

- Controleer dat alleen de nieuwe collector actief is, de bewaargrenzen en FreeTier/AutoPause onveranderd zijn en externe AI uitstaat.
- Voer de repositorytool tools/RecipeLibrary.PrivacyCleanup eerst zonder --apply uit. Alleen analytics/traffic op het bekende Cosmos-account wordt benaderd. Gebruik een tijdelijke, database-scoped eigen datarol als dat nodig is; verwijder die in finally. Lees of wijzig geen recepten-SQL voor deze opschoning.
- Pas daarna legacyVisitor-redactie toe, met ETag-controle en behoud van de oorspronkelijke absolute TTL. Controleer na afloop dat geen niet-lege Visitor-waarde resteert. De tool toont uitsluitend aantallen, nooit de waarden of credentials. Leg uitkomst en datum bij de PR vast. Recente back-upversies vervallen volgens de bestaande 8 uur retentie.
- Controleer de privacyvoorkeur en cookies in een schone browser op zowel de statische als applicatiehost; geen rawcookie- of sleutelwaarde bewaren. Bevestig dat uitschakelen geen SQL-start veroorzaakt en noodzakelijke diagnostiek niet onbedoeld breekt.

## Periodieke controle en wijzigingen

Controleer jaarlijks en na relevante wijzigingen de cookie-/netwerkstromen, bereikbaarheid van het privacycontact, bewaartermijnen, operatorrechten en Microsoft-subverwerkers-/DPA-wijzigingen. Herzie eerder bij incidenten, accountprofilering, een andere regio/provider, externe AI, derdepartijbeelden, gepersonaliseerde campagnelinks, tracking of langere bewaring. Houd de informatie op beide hosts gelijk en neem privacybestanden mee in de statische build. Het privacydocument vervangt deze operationele verantwoordelijkheid niet.
