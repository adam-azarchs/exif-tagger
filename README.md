
[![Build status](https://ci.appveyor.com/api/projects/status/3gk3h9f3dl584w3w?svg=true)](https://ci.appveyor.com/project/adam-azarchs/exif-tagger)

[Download the latest release](https://github.com/adam-azarchs/exif-tagger/releases/download/v0.0.2/PhotoTagger.zip)

# PhotoTagger

This is intended to be a simple tool to edit the interesting parts of jpeg
photos' [exif metadata](http://www.cipa.jp/std/documents/e/DC-008-2012_E.pdf).

Currently the product is incomplete, but the eventual intent is for it to
be able to perform the following edits:

* "Title" field.  This field is a caption which services such as Google Photos
automatically read into their caption field.  Thus, putting captions in your
photos in this field permits avoiding duplicating work captioning photos in
upload services as well as your local hard drive copy.
* "Author" field.  Many cameras set this automatically, but if you share a
camera with someone it's potentially nice to be able to edit the tag.
* "Date Taken" field.  Most services take this tag to be the date and time
used for, e.g., sorting photos by date.  Editing it is primarily valuable for
shifting a set of photos all by the same amount of time (for example if one
forgets to apply daylight savings time to their camera's clock), but it can
also be valuable for scans of film photos.
* GPS latitude/longitude.

# PhotoCull
PhotoCull is a tool for choosing the best images from a set of similar ones.
It is intended for people who, like me, tend to take potentially dozens of
pictures of the same thing and hope one of them comes out well in the end.
It helps the process of picking that "one good one" by allowing you to
compare them in pairs, side-by-side, until you're down to just the ones you
want to keep.
