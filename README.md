# ... With Calendars
A proposal for low-effort event publicity. You maintain your own public calendar (ical or google) and it integrates with other technologies: website, email, dancers’ calendars, booking, and other tools, etc.
## Public Calendar Specification ([example](https://calendar.google.com/calendar/embed?src=0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com&ctz=America%2FNew_York))
* Title or description **Should** include **keywords** for event type: (“contra” if it’s a contra dance)
* Events **Must** have a **start/end time** and **location**
* Description **Should** include dance **details like a flyer**: venue, cost, schedule, parking, policies, contact info, website. Use basic html formatting: lists, bold, font size.
## Automated Website ([example](www.commonfloorcontra.dance))
* Automatic [Next Event](https://github.com/blackboxlogic/CommonFloorContra.Dance/blob/main/ProxyByRegex/LoadEvents.js) section
* Calendar of all future events
* Links to add this dance calendar into dancer’s personal calendar
* Email subscribe form
* Performer Booking page (automatic list of events still seeking performers)
* [Analytics](https://cfcd.goatcounter.com) (help predict event attendance)
* Link [Other Dance Series]((https://github.com/blackboxlogic/CommonFloorContra.Dance/blob/main/ProxyByRegex/LoadOtherSeries.js))
## Automated Email
* Setup an email subscription service (like mailchimp) with an email beamer feature (an email received is sent to all subscribers).
* Create an app password for an account to send the email
* Create a json file like
```
{
  "Name": "Common Floor Contra Dance",
  "Website": "https://commonfloorcontra.dance",
  “CalendarUrl": "https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com/public/basic.ics",
  "EmailDestination": "[redacted]@inbound.mailchimp.com",
  "GmailSender": "info@commonfloorcontra.dance",
  "GmailSenderUser": "admin@commonfloorcontra.dance",
  "GmailSenderAppPassword": "[redacted]",
  "LightColor": "#CBF0FF",
  "DarkColor": "#00374A",
  "PopColor": "#FFC704"
}
```
## Personal Calendar Integration
* Dancers can add your dance calendar into their personal calendars using links (for [Google](https://calendar.google.com/calendar/render?cid=https%3A%2F%2Fcalendar.google.com%2Fcalendar%2Fical%2F0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%2540group.calendar.google.com%2Fpublic%2Fbasic.ics) or [Outlook/Apple](https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com/public/basic.ics)) from emails or your website. Their calendar will show changes you make to your dance calendar eliminating the need for flyers.
## How To: [Booking Tool and Tour Planner]() (in progress)
* **Organizers**, if you want performers to easily find your events seeking talent:
  * Add your dance series with a calendar property to [tryContra.com](trycontra.com)
  * Event description **Should** contain the keyword “**band tbd**” if you want a band, “**caller tbd**” if you want a caller, “**sound tbd**” if you want a sound engineer.
  * Event description **Should** contain a link to booking info page (contact, pay, location, audience description, schedule, housing, how far ahead you book) which can be removed once positions are filled.
  * When performers are booked, events **Must** be **updated**, and **should** contain **links** to the performer websites, if they exist.
  * Keywords can be any variation of capitalization, spacing or punctuation (for example “band tbd” and “Band:TBD” are equivalent)
* **Performers**, to find events:
  * Add filter [contains/doesn’t contain] [key], hide it, flag it
  * Hide not contains “*contra*”
  * Hide not contains “*maine*”
  * Flag Red contains “*band tbd*”
* **Dancers**, to plan a trip

This project is being developed by Alex Hennings and has been partially funded by a generous grant from [CDSS](https://cdss.org/).