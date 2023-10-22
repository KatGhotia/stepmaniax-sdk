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
- Ask for sensor updates on every firmware event loop (seems to be ~15-16ms from testing). This gives us 1000ms/16ms = 60fps updates to our bars. We cannot get higher than this without firmware suport.
- Do this by asking for two updates quickly back to back and expecting the firmware to send back two packets - this way it's always queueing up a packet to send us. Ensure we always have one extra in-flight request for data to ensure timely updates.
- Ensure that the main event loop does not sleep for longer than one firmware event loop if we are expecting sensor test data. This was the main reason that the vanilla version updates so slowly, the main thread waits to ask for too long. In the original case, there was about 95ms between most requests, leading to 1000ms/95 = about 10fps on the bars.
- Fun fact: the capability for this to happen was discovered by the main loop sometimes being able to ask for test sensor data much more frequently than usual. I'm not sure when/why this happened but it was likely to do with some complicated light setups causing the main thread to not block for long, allowing for more timely test sensor data requests.

### Added a debug console to the main app when building in debug mode
- Very useful for debugging things. Can now add `Log()` calls in the SDK project and see the output.
- Disable printf() calls inside log for extra performance if not building for Debug target
- Add another way to write log lines that will work for Release target builds to allow debugging performance based things that may act differently in optimised Release code
  - To use this, `#define NDEBUGLOGGING` in `Helpers.cpp` and make sure the console is registered inside the C# project (normally gated by checking `#ifdef _DEBUG`)
