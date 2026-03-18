// Loads the next upcoming event from a Google Calendar and populates elements on the page.
//
// Example usage:
// <script src="https://cfcdcalendarfunctionappservice.azurewebsites.net/api/LoadEventsScript"
//   data-date-id="text18"
//   data-summary-id="text19"
//   data-description-id="text20"
//   data-calendar-id="0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4@group.calendar.google.com">
//   data-months-ahead="13">
// </script>

(async function () {
    const currentScript = document.currentScript;
    const dateID = currentScript.dataset.dateId;
    const summaryID = currentScript.dataset.summaryId;
    const descriptionID = currentScript.dataset.descriptionId;
    const calendarID = currentScript.dataset.calendarId;
    const months = currentScript.dataset.months;

    const url = "https://cfcdcalendarfunctionappservice.azurewebsites.net/api/GetNextEventsJSON?contains=contra&months=" + months + "&url=https://calendar.google.com/calendar/ical/" + calendarID + "/public/basic.ics";

    const response = await fetch(url);
    var dances = JSON.parse(await response.text());

    var nextDance = dances[0];

    const dateFormatter = new Intl.DateTimeFormat("en-US", {
        weekday: "long",
        year: "numeric",
        month: "long",
        day: "numeric"
    });

    document.getElementById(dateID).innerHTML = dateFormatter.format(Date.parse(nextDance.start));
    document.getElementById(summaryID).innerHTML = nextDance.summary;
    document.getElementById(descriptionID).innerHTML = nextDance.description?.replaceAll("\n", "<br>")
        ?.replaceAll("<ul>", "<ul style='list-style: inside; margin-left: 20px'>")
        ?.replaceAll("<b>", "<b style='font-weight: bolder'>");
})();
