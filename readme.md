## CoApp

CoApp is currently undergoing a bit of a cleanup on GitHub.

This is the "core" project. There is a solution in here which can be built without the Windows SDK/DDK but 
following the instructions in https://github.com/coapp/coapp.org/wiki/Setting-Up-Your-CoApp-Development-Environment 
is still recommended.

If anything doesn't make sense, documentation looks incomplete - log an issue https://github.com/coapp/coapp.org/issues
(@voltagex is looking after some documentation as of December 2011)

### NOTES about committing
This project uses submodules, so it's important to make sure that you update the submodules in ./ext before you commit code to this project.

You can do this from the command prompt: `for /d %v in (ext\*) do ( pushd %v & git pull & popd )`