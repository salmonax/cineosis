## Cineosis

> *He adjusts the image on the electronic oscilloscope where we see a
horizontal line tracing out the pulse variations. Steve is sitting in
front of the sequence generator: a keyboard that triggers hertzian
waves, which it sends over to the phytoplankton sample.*
>
> STEVE<br>
> Let’s party.
<p align="center">
  <img width="100%" src="https://user-images.githubusercontent.com/2810775/164764027-6126dd19-8dc0-4396-8c16-bbb997d008fd.gif">
</p>


### Introduction

This is a mixed reality video player with the following features:


  1. __Passthrough.__ Without which, it wouldn't really be "mixed" reality.
  2. __Masking.__ Four masking modes: difference, color-biased difference, manual color selection, and matte masking. Both difference masks work automatically, color-masking needs colors to be selected with the color-picker, and the matte mask requires a PNG with the same filename to be in the same folder.
  4. __Trigger Chords__. A vast assortment of tweaks available through a confusing, branching, trigger-based control scheme, with no GUI except a wall of unlabeled, changing numbers. (See InputController.cs for bindings.)
  5. __File System Access.__ Videos *can* go in the APK (see ClipProvider implementation), but currently need to have their paths added to CoreConfig.cs.
  Pretty easy to add a hack to get them from the web, also (see the AndroidManifest.xml)
  6. __Anti-distortion.__ A togglable AutoShiftMode that doesn't work very well yet, but which greatly reduces the amount of distortion at high zoom.
  7. __Smart Resizing.__ A means to simultaneously adjusts zoom and horizontal offset, so that the depth stays the same as the effective size changes.

### Caveats
Usability isn't its strong suit, so prepare to enter an alien world.

### Usage
Dump it into a Unity project and uncomment CoreConfig.Example.cs. Deploy to Oculus device. (Note: APK link will be provided as soon as file browsing is supported.)

### License

This is provided for educational purposes only and not really for use in any form.