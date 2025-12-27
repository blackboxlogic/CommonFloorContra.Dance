import ICAL from "https://unpkg.com/ical.js/dist/ical.min.js";
// https://fullcalendar.io/docs/initialize-es6
import { Calendar } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
// import interactionPlugin from '@fullcalendar/interaction'
// import listPlugin from '@fullcalendar/list'

// Example:
// document.addEventListener('DOMContentLoaded', function() {
//         const calendarEl = document.getElementById('calendar')
//         const calendar = new Calendar(calendarEl, {
//           plugins: [dayGridPlugin],
//           headerToolbar: {
//             left: 'prev,next today',
//             center: 'title',
//             right: 'dayGridMonth,timeGridWeek,timeGridDay,listWeek'
//           }
//         })
//         calendar.render()
//       })

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
              const promises = dances
                .filter(dance => dance.city.endsWith(' ME'))
                .filter(dance => dance.icals)
                .flatMap(dance => dance.icals)
                .map(url => fetch(proxyUrl + encodeURIComponent(url)).then(response => {
                  if (!response.ok) {
                    throw new Error('Bad response: ' + url + ' Status: ' + response.status + ' Status Text: ' + response.statusText);
                  }
                  return response.text();
                }));

              Promise.all(promises)
                .then(icalStrings => {
                  let events = [];
                  icalStrings.forEach(icalString => {
                    if (icalString) {
                        try {
                            const jcalData = ICAL.parse(icalString);
                            const component = new ICAL.Component(jcalData);
                            const vevents = component.getAllSubcomponents('vevent');
                            vevents.forEach(vevent => {
                                const event = new ICAL.Event(vevent);
                                // TODO: add event filtering here
                                if (event.summary) { // Only add events with a title
                                  events.push({
                                      title: event.summary,
                                      start: event.startDate.toJSDate(),
                                      end: event.endDate.toJSDate(),
                                      extendedProps: {
                                        location: event.location,
                                        description: event.description
                                      }
                                  });
                                }
                            });
                        } catch (e) {
                            console.error('Error parsing iCal string:', e);
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
        }
      });
      calendar.render();
    }
  }
};
