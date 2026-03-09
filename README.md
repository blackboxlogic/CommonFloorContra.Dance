# With Calendars
## Summary
A proposal for each dance series to maintain its own public calendar (ical or google) which can integrate with other technologies: websites, automated email, dancers’ calendars, booking, and other tools, etc.
## Public Calendar Specification ([example](https://calendar.google.com/calendar/embed?src=0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com&ctz=America%2FNew_York))
* All posted events **Must** be open to the **public**
* Title or description **Should** include **keywords** for event type: (“contra” if it’s a contra dance)
* Events **Must** has a start/end **time**
* Events **Must** have a **location** (what about online events?)
* Description **Should** include dance **details** (like a flyer): location, cost, schedule, parking, policies, contact info, or at least a link to your dance series’s primary website
* Title of events that are tentative **should** contain **“TENTATIVE“**
* Title of events that are cancelled **should** contain **“CANCELLED”** (or delete the event)
## Automated Website ([example](www.commonfloorcontra.dance))
* Auto-updating “Next Event” section
* Calendar of all future events
* Links to add this dance calendar into dancer’s personal calendar
* Email subscribe form
* Performer Booking Info page
* [Analytics](cfcd.goatcounter.com): can help predict event attendance
* Links to other local dance series
* You’ll need to choose/purchase a domain, configure the DNS, build and host a website
## Personal Calendar Integration
* Dancers can add your dance calendar into their personal calendars using links (for [Google](https://calendar.google.com/calendar/render?cid=https%3A%2F%2Fcalendar.google.com%2Fcalendar%2Fical%2F0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%2540group.calendar.google.com%2Fpublic%2Fbasic.ics) or [Outlook/Apple](https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com/public/basic.ics)) from emails or your website. Their calendar will show changes you make to your dance calendar.
## Automated Email
* Setup an email subscription service (like mailchimp) with an email beamer feature (an email received is sent to all subscribers).
* Create an app password for an account to send the email
* Create a json file like
```
{
  "SeriesName": "Common Floor Contra Dance",
  "SeriesWebsite": "https://commonfloorcontra.dance",
  “SeriesCalendarUrl": "https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com/public/basic.ics",
  "SeriesEmailDestination": "[redacted]@inbound.mailchimp.com",
  "SeriesGmailSender": "info@commonfloorcontra.dance",
  "SeriesGmailSenderUser": "admin@commonfloorcontra.dance",
  "SeriesGmailSenderAppPassword": "[redacted]",
  "LightColor": "#CBF0FF",
  "DarkColor": "#00374A",
  "PopColor": "#FFC704"
}
```
## How To: [Booking Tool and Tour Planner]() (in progress)
* **Organizers**, if you want performers to easily find your events seeking talent:
  * Add your dance series with a calendar property to [tryContra.com](trycontra.com)
  * Event description **Must** contain the keyword “band tbd” if you want a band
  * Event description **Must** contain the keyword “caller tbd” if you want a caller
  * Event description **Must** content the keyword “sound tbd” if you want a sound engineer
  * Event description **Should** contain a link to booking info page (contact, pay, location, audience description, schedule, housing, how far ahead you book) which can be removed once positions are filled.
  * When performers are booked, events **Must** be **updated**, and **should** contain **links** to the performer websites, if they exist.
  * Keywords can be any variation of capitalization, spacing or punctuation (for example “band tbd” and “Band:TBD” are equivalent)
* **Performers**, to find events:
  * Add filter [contains/doesn’t contain] [key], hide it, flag it
  * Hide contains “*cancelled*”
  * Hide not contains “*contra*”
  * Hide not contains “*maine*”
  * Flag Red contains “*band tbd*”

