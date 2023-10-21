This is the SDK for StepManiaX platform development.

See [the StepManiaX website](https://stepmaniax.com) and the [documentation](https://steprevolution.github.io/stepmaniax-sdk/)
for info.

SDK support: [sdk@stepmaniax.com](mailto:sdk@stepmaniax.com)

---

## Changes from fork

### Showing sensitivity threshold values for P2 controller
- Fix bug where P2 controller didn't show thresholds on sensitivity panel and only one SMX was connected
  - This could be worked around by making every panel a P1 panel but this requires changing some physical jumpers which is annoying

### More respnsive sensitivity threshold slider display
- Ask for sensor updates every 30ms rather than every 2s for more responsive sensor bars while the UI is open (more formally, while test mode is enabled on the SMX)
  - This gives us 1000/30 ~= 33 fps on our bars. We could go higher, but all the code does is *ask* for updates more often, it is not guaranteed to receive them from the SMX *and* I was worried about potentially overloading the SMX and didn't want to do all the testing involved.
  - There was a weird edge case I never worked out properly where the SMX would respond with data much more frequently on rare occasions where two SMX pads were plugged in and even rarer ones with one SMX pad plugged in. I don't have two, and I didn't want to emulate it, so I never found the exact reason why this could happen. These more frequent updates caused the bars to look more responsive, which looked nice, which led to the development of this feature.
    - Note that the only way for an app to receive test sensor data (i.e. the raw values) from an SMX is to ask for it. The way the SDK works right now is that it asks for test sensor data, writes down that it asked, and then when it receives a response with test data it clears that it asks so it can ask again on the next loop iteration. It will ask again if X ticks pass (2000 before the change) and it has still received no test data.
    - Something about the edge case means that the SMXes were responding much quicker than normal, whereas to get these quicker updates the implemented workaround instead just asks for data more often. Why do we have to ask more often to get more timely answers? This seems like something that could only easily be cleared up by examining the firmware.
    - Maybe there's a race condition somewhere where asking once and then immediately receiving the packets causes problems, but asking multiple times allows the message to be properly received? I suspect there's a race condition *somewhere* causing successful responses to be discarded
