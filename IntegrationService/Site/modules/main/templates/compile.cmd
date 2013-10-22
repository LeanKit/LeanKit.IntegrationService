@echo off

SET _dir=%1

:: Remove quotes
   SET _dir=###%_dir%###
   SET _dir=%_dir:"###=%
   SET _dir=%_dir:###"=%
   SET _dir=%_dir:###=%


SET _tmpDir=%_dir%client\modules\main\templates\
SET _outputFile=%_dir%client\generated\main-templates.js

handlebars "%_tmpDir%body.html" "%_tmpDir%footer.html" "%_tmpDir%header.html" -m -n "Handlebars.main" -f "%_outputFile%"

