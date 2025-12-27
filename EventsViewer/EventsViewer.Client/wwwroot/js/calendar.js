import ICAL from "https://unpkg.com/ical.js/dist/ical.min.js";
// https://fullcalendar.io/docs/initialize-es6
import { Calendar } from 'https://cdn.jsdelivr.net/npm/@fullcalendar/core@6.1.20/dist/index.esm.js';
import dayGridPlugin from 'https://cdn.jsdelivr.net/npm/@fullcalendar/daygrid@6.1.20/dist/index.esm.js';
import timeGridPlugin from 'https://cdn.jsdelivr.net/npm/@fullcalendar/timegrid@6.1.20/dist/index.esm.js';
// import timeGridPlugin from 'https://cdn.jsdelivr.net/npm/@fullcalendar/list@6.1.20/index.js';

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
          fetch('data/dances.json')
            .then(response => response.json())
            .then(dances => {
              const promises = dances
                .filter(dance => dance.icals)
                .flatMap(dance => dance.icals)
                .slice(0, 5)
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
