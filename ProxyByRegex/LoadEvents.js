// Loads the next event from a iCal calendar and populates details into elements on your page.
// TODO: emit <script type="application/ld+json">

/* Example usage:
<script src="https://cfcdcalendarfunctionappservice.azurewebsites.net/api/LoadEventsScript"
  data-ical-link="https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4@group.calendar.google.com/public/basic.ics"
  // All following parameters are optional, if left off then that section won't be populated:
  // The IDs of elements to receive event details:
  data-date-id="next-dance-date"
  data-time-id="next-dance-time"
  data-summary-id="next-dance-title"
  data-description-id="next-dance-description"
  data-location-id="next-dance-location" // Should be a <a href> tag, will get a google maps link and text.
  data-list-tbd-id="tbd-list"> // Should be a <div> with a single <ul> or <ol> in it, will get filled with <li> for each "TBD" event (has "tbd" in the description, looking to hire caller/band).
  // Configuration
  data-months-ahead="13" // How far ahead in time to look (Default is 12)
  data-filter="contra" // Only returns events with this phrase in the summary or description
  data-force-description-styles="true" // Inlines bulleted-list and bold styles (in case your site's css suppresses <ul> and <b>).
  data-emit-schema="true"> // Emits a <script type="application/ld+json"> with schema.org DanceEvent data for the next event, improses SEO.
</script>
<div>
  <h2 id="next-dance-title">Loading title…</h2>
  <h2 id="next-dance-date" style="display: inline">Loading date…</h2> at <h2 id="next-dance-time" style="display: inline">Loading time…</h2>
  <h3> Venue: <a href="" id="next-dance-location">Loading venue…</a></h3>
  <h3 id="next-dance-description">Loading description…</h3>
  <div id="tbd-list">We're booking performers for these dances:<ul></ul></div>
</div>
*/

{
    const currentScript = document.currentScript;
    const dateID = currentScript.dataset.dateId;
    const timeID = currentScript.dataset.timeId;
    const summaryID = currentScript.dataset.summaryId;
    const descriptionID = currentScript.dataset.descriptionId;
    const icalLink = currentScript.dataset.icalLink;
    const months = currentScript.dataset.monthsAhead;
    const filter = currentScript.dataset.filter;
    const locationID = currentScript.dataset.locationId;
    const forceDescriptionStyles = currentScript.dataset.forceDescriptionStyles;
    const listTbdID = currentScript.dataset.listTbdId;
    const emitSchema = currentScript.dataset.emitSchema;

    (async function () {

        const containsParam = filter ? "&contains=" + filter : "";
        const monthsParam = months ? "&months=" + months : "";
        const icalLinkParam = "url=" + icalLink
        const url = "https://cfcdcalendarfunctionappservice.azurewebsites.net/api/GetNextEventsJSON?" + icalLinkParam + monthsParam + containsParam;

        const response = await fetch(url);
        var dances = JSON.parse(await response.text());

        if (dances.length == 0) return;
        var nextDance = dances[0];

        const dateFormatter = new Intl.DateTimeFormat("en-US", {
            weekday: "long",
            year: "numeric",
            month: "long",
            day: "numeric"
        });

        const timeFormatter = new Intl.DateTimeFormat("en-US", {
            hour: "numeric",
            minute: "2-digit",
            hour12: true
        });

        if (dateID) document.getElementById(dateID).innerHTML = dateFormatter.format(Date.parse(nextDance.start));
        if (timeID) document.getElementById(timeID).innerHTML = timeFormatter.format(Date.parse(nextDance.start));
        if (summaryID) document.getElementById(summaryID).innerHTML = nextDance.summary;
        if (descriptionID) {
            if (forceDescriptionStyles) {
                document.getElementById(descriptionID).innerHTML = nextDance.description?.replaceAll("\n", "<br>")
                    ?.replaceAll("<ul>", "<ul style='list-style: inside; margin-left: 20px'>")
                    ?.replaceAll("<b>", "<b style='font-weight: bolder'>");
            } else {
                document.getElementById(descriptionID).innerHTML = nextDance.description?.replaceAll("\n", "<br>");
            }
        }
        if (locationID) {
            document.getElementById(locationID).innerHTML = nextDance.location;
            document.getElementById(locationID).href = "https://maps.google.com/maps?hl=en&q=" + nextDance.location;
        }

        if (emitSchema) {
            const schemaScript = document.createElement("script");
            schemaScript.type = "application/ld+json";
            schemaScript.textContent = JSON.stringify({
                "@context": "https://schema.org",
                "@type": "DanceEvent",
                "name": nextDance.summary,
                "startDate": nextDance.start,
                "endDate": nextDance.end,
                "location": {
                    "@type": "Place",
                    "name": nextDance.location,
                    "address": nextDance.location
                },
                "description": nextDance.description?.replace(/<[^>]*>/g, '')
            });
            document.head.appendChild(schemaScript);
        }

        if (listTbdID) {
            dances = dances.filter(dance => dance.summary.toLowerCase().includes("tbd")); //.sort((a, b) => new Date(a.start) - new Date(b.start))
            const list = document.getElementById("list01").children[0];
            list.innerHTML = "";
            dances.forEach(dance => {
                const paragraph = document.createElement("p");
                const listItem = document.createElement("li");
                const summary = document.createTextNode(dateFormatter.format(Date.parse(dance.start)) + " ~ " + dance.summary);
                listItem.appendChild(paragraph);
                paragraph.appendChild(summary);
                list.appendChild(listItem);
            });
        }
    })();
}

//# sourceURL=LoadEvents.js
