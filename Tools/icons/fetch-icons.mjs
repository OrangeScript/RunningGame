import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Resvg } from '@resvg/resvg-js';

const root = process.env.RUNNING_PROJECT_ROOT ?? path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const icons = ['flame','droplets','zap','wind','sprout','radio-tower','crosshair','copy','move-up-right',
  'repeat-2','focus','footprints','shield','heart-pulse','circle-dashed','sparkles','swords','split',
  'orbit','snowflake','heart','eraser','syringe','shield-check','cloud-lightning','feather',
  'battery-charging','droplet','star','weight','bomb','cloud-rain','mountain','gauge','target','eclipse'];
const source = path.join(root,'Assets/ARt/Icons/Lucide');
const output = path.join(root,'Assets/Resources/ItemIcons');
await fs.mkdir(source,{recursive:true}); await fs.mkdir(output,{recursive:true});
for (const key of icons) {
  const response = await fetch(`https://api.iconify.design/lucide/${key}.svg`);
  if (!response.ok) throw new Error(`${key}: HTTP ${response.status}`);
  const svg = (await response.text()).replaceAll('currentColor','#ffffff');
  await fs.writeFile(path.join(source,`${key}.svg`),svg);
  const image = new Resvg(svg,{fitTo:{mode:'width',value:128}}).render().asPng();
  await fs.writeFile(path.join(output,`${key}.png`),image);
  process.stdout.write(`${key}\n`);
}
const license = await fetch('https://raw.githubusercontent.com/lucide-icons/lucide/main/LICENSE');
if (!license.ok) throw new Error('Lucide license download failed');
await fs.writeFile(path.join(source,'LICENSE.txt'),await license.text());
