// Loads the next event from a iCal calendar and populates details into elements on your page.
// In carrd, script element stripped `data-` attributes. Use `style=hidden` on carrd embed element.

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
    const dateIDs = (currentScript.dataset.dateId ?? '').split(',').map(s =>s.trim()).filter(Boolean);
    const timeIDs = (currentScript.dataset.timeId ?? '').split(',').map(s => s.trim()).filter(Boolean);
    const summaryIDs = (currentScript.dataset.summaryId ?? '').split(',').map(s => s.trim()).filter(Boolean);
    const descriptionIDs = (currentScript.dataset.descriptionId ?? '').split(',').map(s => s.trim()).filter(Boolean);
    const locationIDs = (currentScript.dataset.locationId ?? '').split(',').map(s => s.trim()).filter(Boolean);
    const icalLink = currentScript.dataset.icalLink;
    const months = currentScript.dataset.monthsAhead;
    const filter = currentScript.dataset.filter;
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

        for (var i = 0; i < dances.length; i++) {
            if (i < dateIDs.length) document.getElementById(dateIDs[i]).innerHTML = dateFormatter.format(Date.parse(dances[i].start));
            if (i < timeIDs.length) document.getElementById(timeIDs[i]).innerHTML = timeFormatter.format(Date.parse(dances[i].start));
            if (i < summaryIDs.length) document.getElementById(summaryIDs[i]).innerHTML = dances[i].summary;
            if (i < descriptionIDs.length) {
                if (forceDescriptionStyles) {
                    document.getElementById(descriptionIDs[i]).innerHTML = dances[i].description?.replaceAll("\n", "<br>")
                        ?.replaceAll("<ul>", "<ul style='list-style: inside; margin-left: 20px'>")
                        ?.replaceAll("<b>", "<b style='font-weight: bolder'>");
                } else {
                    document.getElementById(descriptionIDs[i]).innerHTML = dances[i].description?.replaceAll("\n", "<br>");
                }
            }
            if (i < locationIDs.length) {
                document.getElementById(locationIDs[i]).innerHTML = dances[i].location;
                document.getElementById(locationIDs[i]).href = "https://maps.google.com/maps?hl=en&q=" + dances[i].location;
            }

            if (emitSchema) {
                const schemaScript = document.createElement("script");
                schemaScript.type = "application/ld+json";
                schemaScript.textContent = JSON.stringify({
                    "@context": "https://schema.org",
                    "@type": "DanceEvent",
                    "name": dances[i].summary,
                    "startDate": dances[i].start,
                    "endDate": dances[i].end,
                    "location": {
                        "@type": "Place",
                        "name": dances[i].location,
                        "address": dances[i].location
                    },
                    "description": dances[i].description?.replace(/<[^>]*>/g, '')
                });
                document.head.appendChild(schemaScript);
            }
        }

        if (listTbdID) {
            dances = dances.filter(dance => dance.summary.toLowerCase().includes("tbd")); //.sort((a, b) => new Date(a.start) - new Date(b.start))
            const list = document.getElementById(listTbdID).children[0];
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
