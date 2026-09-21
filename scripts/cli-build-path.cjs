const path = require('node:path');

// Browser verification has no MSBuild context. Select explicitly; never pick a
// binary by whichever Debug/Release artifact happens to exist on this machine.
const configuration = process.env.SOUNDSCRIPT_BUILD_CONFIGURATION || 'Release';
if (!['Release', 'Debug'].includes(configuration)) throw new Error('SOUNDSCRIPT_BUILD_CONFIGURATION must be Release or Debug');
module.exports = path.resolve(__dirname, '..', 'src', 'SoundScript.Cli', 'bin', configuration, 'net10.0', 'soundscript.dll');
