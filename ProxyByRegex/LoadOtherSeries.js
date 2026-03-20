// Loads and links other local dance series into elements on your page (from trycontra.com)

/* Example usage:
<script src="https://cfcdcalendarfunctionappservice.azurewebsites.net/api/LoadOtherSeriesScript"
  data-state="ME"
  data-list-id="seriesList"
</script>
<p id="seriesList">Loading other local dance series…</p>
*/

(async function () {
    const currentScript = document.currentScript;
    const state = currentScript.dataset.state;
    const listId = currentScript.dataset.listId;

    const response = await fetch("https://www.trycontra.com/dances_locs.json");
    var json = await response.text();
    var dances = JSON.parse(json);
    dances = dances.filter(dance => dance.city.endsWith(" " + state) && !dance.inactive);
    dances.sort((a, b) => a.lat + a.lng - b.lat - b.lng);

    const listContainer = document.getElementById(listId);
    listContainer.innerHTML = "";

    dances.forEach((dance, index, array) => {
        const link = document.createElement("a");
        link.href = dance.url;
        link.textContent = dance.city.replace(" " + state, "");
        listContainer.appendChild(link);
        if (index < array.length - 1) {
            listContainer.appendChild(document.createTextNode(", "));
        }
    });
})();

//# sourceURL=LoadOtherSeries.js
