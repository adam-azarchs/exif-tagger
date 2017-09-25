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
