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
  },
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
        events: function(fetchInfo, successCallback, failureCallback) {
          const proxyUrl = window.appConfig.proxyUrl;
          fetch('https://www.trycontra.com/dances_locs.json')
            .then(response => response.json())
            .then(dances => {
              const filteredDances = dances
                .filter(dance => dance.city.endsWith(' ME'))
                .filter(dance => dance.inactive !== 'true')
                .filter(dance => dance.icals);

              filteredDances.forEach(dance => {
                dance.color = stringToColor(dance.url);
              });

              const promises = filteredDances.map(dance =>
                fetch(proxyUrl + encodeURIComponent(dance.icals[0]))
                  .then(response => {
                    if (response.ok) {
                      return response.text().then(text => {
                        dance.icalString = text;
                        return dance;
                      });
                    } else {
                      console.warn(`Failed to fetch ical for ${dance.url}`);
                      return dance; // still return dance so we don't have gaps in the promise array
                    }
                  })
              );

              Promise.all(promises)
                .then(dancesWithIcal => {
                  let events = [];
                  dancesWithIcal.forEach(danceSeries => {
                    if (danceSeries.icalString) {
                        try {
                            const jcalData = ICAL.parse(danceSeries.icalString);
                            const component = new ICAL.Component(jcalData);
                            const vevents = component.getAllSubcomponents('vevent');
                            vevents.forEach(vevent => {
                                const event = new ICAL.Event(vevent);
                                // TODO: add better event filtering
                                if (event.summary && event.summary.toLowerCase().includes("contra")
                                || event.description && event.description.toLowerCase().includes("contra")
                                || true) {
                                  events.push({
                                      title: event.summary,
                                      start: event.startDate.toJSDate(),
                                      end: event.endDate.toJSDate(),
                                      backgroundColor: danceSeries.color || '#3788d8',
                                      borderColor: danceSeries.color || '#3788d8',
                                      extendedProps: {
                                        location: event.location,
                                        description: event.description,
                                        danceSeries: danceSeries
                                      }
                                  });
                                }
                            });
                        } catch (e) {
                            console.error('Error parsing iCal string for ' + danceSeries.url, e, danceSeries.icalString);
                        }
                    }
                  });
                  successCallback(events);
                })
                .catch(error => {
                    console.error('Error fetching iCal files:', error);
                    failureCallback(error);
                });
            })
            .catch(error => {
                console.error('Error fetching dances.json:', error);
                failureCallback(error);
            });
        },
        eventDidMount: function(info) {
          var event = info.event;
          var content = '<b>' + event.title + '</b><br>' +
                        'Start: ' + event.start.toLocaleString() + '<br>' +
                        'End: ' + (event.end ? event.end.toLocaleString() : 'N/A') + '<br>' +
                        (event.extendedProps.location ?
                          'Location: <a href="https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(event.extendedProps.location) + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.location + '</a><br>'
                          : 'Location: <i>Not specified</i><br>') +
                        'Series: <a href="' + event.extendedProps.danceSeries.url + '" target="_blank" rel="noopener noreferrer">' + event.extendedProps.danceSeries.url.replace(/^https?:\/\//, '') + '</a><br>' +
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
