import ICAL from "@ical.js";
// https://fullcalendar.io/docs/initialize-es6
import { Calendar } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import tippy from 'tippy.js';

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

loadCss('https://unpkg.com/tippy.js@6/dist/tippy.css');
loadCss('https://unpkg.com/tippy.js@6/themes/light-border.css');
const proxyUrl = window.appConfig.proxyUrl;

async function getEvents(org) {
  if(org._events) return org._events;

  const responses = await Promise.all(
    org.icals.map(icalUrl =>
      fetch(proxyUrl + encodeURIComponent(icalUrl))
    )
  );

  const eventArrays = await Promise.all(
    responses
      .filter(r => r.ok)
      .map(async response => {
        const icalString = await response.text();
        const icalData = ICAL.parse(icalString);
        const component = new ICAL.Component(icalData);
        const events = component.getAllSubcomponents('vevent');
        return events.map(vevent => new ICAL.Event(vevent));
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
      //org._events = [];
      if(org.icals) org.getEvents = () => getEvents(org);
    });

    return organizations;
}
const organizationsPromise = loadOrganizations();

window.calendarInterop = {
  init: function (elementId) {
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
        events: async function(fetchInfo, successCallback, failureCallback) {

          const organizations = await organizationsPromise;
          let events = [];

          await Promise.all(
            organizations.filter(org => org.getEvents && org.state === 'ME')
              .map(async org => {
                const orgEvents = await org.getEvents();

                const orgEventsFiltered = orgEvents
                  .filter(event => event.summary && event.summary.toLowerCase().includes("contra")
                    || event.description && event.description.toLowerCase().includes("contra"))
                  .map(event => ({
                      title: event.summary,
                      start: event.startDate.toJSDate(),
                      end: event.endDate.toJSDate(),
                      backgroundColor: org.color || '#3788d8',
                      borderColor: org.color || '#3788d8',
                      extendedProps: {
                        location: event.location,
                        description: event.description,
                        organization: org
                      }
                    })
                  );

                  events.push(...orgEventsFiltered); // todo add a new source instead.
              }
            ));

          successCallback(events);
        },

        eventDidMount: function(info) {
          var event = info.event;
          var content = '<b>' + event.title + '</b><br>' +
                        'Start: ' + event.start.toLocaleString() + '<br>' +
                        'End: ' + (event.end ? event.end.toLocaleString() : 'N/A') + '<br>' +
                        (event.extendedProps.location ?
                          'Location: <a href="https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(event.extendedProps.location) + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.location + '</a><br>'
                          : 'Location: <i>Not specified</i><br>') +
                        'Series: <a href="' + event.extendedProps.organization.url + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.organization.url.replace(/^https?:\/\//, '') + '</a><br>' +
                        'Description: ' + event.extendedProps.description || '<i>Not specified</i>';
        
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
      calendar.render();
    }
  }
};
