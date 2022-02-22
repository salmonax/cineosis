## Cineosis


> *He adjusts the image on the electronic oscilloscope where we see a
horizontal line tracing out the pulse variations. Steve is sitting in
front of the sequence generator: a keyboard that triggers hertzian
waves, which it sends over to the phytoplankton sample.*
>
> STEVE<br>
> Let’s party.


### Introduction

This is a mixed reality video player with the following features:


  1. __Passthrough.__ Without which, it wouldn't really be MR.
  2. __Masking.__ Two masking modes: difference and color selection. Difference works automatically, color-masking needs to be invoked.
  4. __Trigger Chords__. A vast assortment of tweaks available through a confusing, branching, trigger-based control scheme, with no GUI except a wall of unlabeled, changing numbers.
  5. __File System Access.__ Videos *can* go in the APK (see ClipProvider implementation), but are provided
  Pretty easy to hack to get them from the web, also (see the AndroidManifest.xml)
  6. __Anti-distortion.__ A togglable AutoShiftMode that doesn't work very well yet, but which greatly reduces the amount of distortion at high zoom.
  7. __Resizing.__ A means to simultaneously adjusts zoom and horizontal offset, so that the depth stays the same as the effective size changes.

### Caveats
Usability isn't its strong suit, so prepare to enter an alien world. Also, it only works with side-to-side 180 videos, not 360 or top-to-bottom 180.

### Usage
Dump it into a Unity project and uncomment CoreConfig.Example.cs. Deploy to Oculus device.

### License

This is provided for educational purposes only and not for commercial or non-commercial use of any form.