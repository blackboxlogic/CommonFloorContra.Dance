// https://github.com/mifi/ical-expander because https://github.com/kewisch/ical.js/issues/285
import IcalExpander from 'ical-expander';
// https://github.com/kewisch/ical.js
import ICAL from "@ical.js";
// https://fullcalendar.io/docs/initialize-es6
import { Calendar } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import tippy from 'tippy.js';

loadCss('https://unpkg.com/tippy.js@6/dist/tippy.css');
loadCss('https://unpkg.com/tippy.js@6/themes/light-border.css');
const proxyUrl = window.appConfig.proxyUrl;

const sixMonthsAgo = new Date(); sixMonthsAgo.setMonth(sixMonthsAgo.getMonth() - 6);
const twoYearsLater = new Date(); twoYearsLater.setFullYear(twoYearsLater.getFullYear() + 2);

// An example dance series (I'm using url like name):
/*
  {
    "annual_freq": 24,
    "city": "Santa Fe NM",
    "icals": [
      "https://calendar.google.com/calendar/ical/folkmads@gmail.com/public/basic.ics"
    ],
    "lat": 35.69,
    "lng": -105.94,
    "url": "http://folkmads.org/events/santa-fe-events/",
    "weekdays": "Saturdays"
  }

  I've added, during processing:
  - color: string (a color derived from the url)
  - state: string (the state code, e.g., "ME")
  - _events: private storage of events once loaded
  - getEvents: async function to load events, null if no ical urls.
*/

function loadCss(href) {
    if (!document.querySelector(`link[href="${href}"]`)) {
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }
}

function stringToColor(str) {
  if (!str) return '#3788d8'; // Default color if string is null or empty
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = str.charCodeAt(i) + ((hash << 5) - hash);
  }
  let color = '#';
  for (let i = 0; i < 3; i++) {
    let value = (hash >> (i * 8)) & 0xFF;
    color += ('00' + value.toString(16)).substr(-2);
  }
  return color;
}

async function getEvents(org) {
  if(org._events) return org._events;

  const responses = await Promise.all(
    org.icals.map(icalUrl =>
      fetch(proxyUrl + encodeURIComponent(icalUrl)) // + '&cache=false'
    )
  );

  const eventArrays = await Promise.all(
    responses
      .filter(r => r.ok)
      .map(async response => {
        const icalString = await response.text();
        const icalExpander = new IcalExpander({ ics: icalString, maxIterations: 100 });
        const expanded = icalExpander.between(sixMonthsAgo, twoYearsLater);
        const allEvents = expanded.events.map(e => ({
            title: e.summary,
            start: e.startDate.toJSDate(),
            end: e.endDate.toJSDate(),
            extendedProps: {
                location: e.location,
                description: e.description,
                organization: org,
                id: e.uid,
            }
        }));

        const allOccurrences = expanded.occurrences.map(o => ({
            title: o.item.summary,
            start: o.startDate.toJSDate(),
            end: o.endDate.toJSDate(),
            extendedProps: {
                location: o.item.location,
                description: o.item.description,
                organization: org,
                id: o.item.uid,
                recurrenceId: o.recurrenceId.toString()
            }
        }));

        return allEvents.concat(allOccurrences);
      })
  );

  org._events = eventArrays.flat();

  return org._events;
}

async function loadOrganizations()
{
  var response = await fetch('https://www.trycontra.com/dances_locs.json');
  var organizations = await response.json();
  organizations = organizations.filter(org => org.inactive !== 'true' && org.icals);

  organizations.forEach(async org => {
      org.color = stringToColor(org.url);
      org.state = org.city.split(' ').pop();
      if(org.icals) org.getEvents = () => getEvents(org);
    });

    return organizations;
}
const organizationsPromise = loadOrganizations();

function orgToEventSource(org, eventFilter) {
  return {
    id: org.url,
    backgroundColor: org.color,
    borderColor: org.color,

    events: async (fetchInfo, successCallback, failureCallback) => {
      try {
        const orgEvents = await org.getEvents();

        const filtered = orgEvents
          .filter(event =>
            (eventFilter === null || event.title && event.title.toLowerCase().includes(eventFilter.toLowerCase())) ||
            (eventFilter === null || event.extendedProps.description && event.extendedProps.description.toLowerCase().includes(eventFilter.toLowerCase()))
          );

        successCallback(filtered);
      } catch (err) {
        failureCallback(err);
      }
    }
  };
}

window.calendarInterop = {
  init: async function (elementId) {
    var calendarEl = document.getElementById(elementId);
    if (calendarEl) {
      var calendar = new Calendar(calendarEl, {
        plugins: [ dayGridPlugin, timeGridPlugin ],
        initialView: 'dayGridMonth',
        headerToolbar: {
          left: 'prev,next today',
          center: 'title',
          right: 'dayGridMonth,timeGridWeek,timeGridDay'
        },

        eventDidMount: function(info) {
          var event = info.event;
          var content = //'IDs: ' + event.extendedProps.id + '-' + event.extendedProps.recurrenceId + '-' + event.extendedProps.sequence  + '<br>' +
                        '<b>' + event.title + '</b><br>' +
                        '<b>Start:</b> ' + event.start.toLocaleString() + '<br>' +
                        '<b>End:</b> ' + (event.end ? event.end.toLocaleString() : 'N/A') + '<br>' +
                        (event.extendedProps.location ?
                          '<b>Location:</b> <a href="https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(event.extendedProps.location) + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.location + '</a><br>'
                          : '<b>Location:</b> <i>Not specified</i><br>') +
                        '<b>Series:</b> <a href="' + event.extendedProps.organization.url + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.organization.url.replace(/^https?:\/\//, '') + '</a><br>' +
                        '<b>Description:</b> ' + event.extendedProps.description || '<i>Not specified</i>';
                        // TODO: links
                        // add this event to your personal calendar
                        // add this series to your personal calendar
                        // add current event filter to your personal calendar
        
          tippy(info.el, {
            content: content,
            allowHTML: true, // Allow HTML content in the tooltip
            trigger: 'click', // How the tooltip is triggered. Can be 'mouseenter focus', 'click', 'manual'
            placement: 'auto', // Preferred placement. E.g., 'top', 'bottom', 'left', 'right'. 'auto' is default.
            arrow: true, // Show an arrow pointing to the element
            theme: 'light-border', // Theme for the tooltip. Tippy comes with 'dark', 'light', 'light-border', 'translucent'
            interactive: true, // Allows the user to hover over and click inside the tooltip
            // --- Other useful properties ---
            // delay: [100, 200], // Delay in milliseconds to show and hide the tooltip [show, hide]
            // duration: [250, 200], // Duration of the show and hide animations
            // animation: 'scale', // Animation type. E.g., 'shift-away', 'shift-toward', 'scale', 'fade'
            // onShow(instance) { /* Code to run before the tooltip shows */ },
            // onHidden(instance) { /* Code to run after the tooltip has hidden */ },
          });
        }
      });

      const urlParams = new URLSearchParams(window.location.search);
      const stateFilter = urlParams.get('states');
      const termFilter = urlParams.get('terms');

      const organizations = await organizationsPromise;
      organizations
        .filter(org => org.getEvents
          && (stateFilter === null || stateFilter.toLowerCase().includes(org.state.toLowerCase())))
        //.filter(org => org.getEvents && org.state === 'ME' && (org.url.includes('surry') || org.url.includes('common')))
        //.forEach(org => { org.icals[0] = "http://localhost:5267/SurrySample.ics"; calendar.addEventSource(orgToEventSource(org, "contra"));});
        .forEach(org => calendar.addEventSource(orgToEventSource(org, termFilter)));

      calendar.render();

    }
  }
};
