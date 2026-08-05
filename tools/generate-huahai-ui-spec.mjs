import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const approvedThemeIds = [
  'rose-purple',
  'cobalt-blue',
  'emerald-cyan',
  'amber-orange',
  'aurora-cyan-purple',
];

const themeColorKeys = [
  'accent',
  'reflection',
  'glassTop',
  'glassBottom',
  'contentLens',
  'focus',
  'text',
  'muted',
];

const argbPattern = /^#[0-9A-F]{8}$/;

export function validateSpec(spec) {
  const errors = [];

  if (!spec || typeof spec !== 'object' || Array.isArray(spec)) {
    return ['spec must be an object'];
  }

  if (!Array.isArray(spec.themes) || spec.themes.length !== 5) {
    return ['themes must contain exactly 5 entries'];
  }

  if (spec.schemaVersion !== 1) errors.push('schemaVersion must be 1');
  if (spec.visualSourceVersion !== '1.0.4') {
    errors.push('visualSourceVersion must be 1.0.4');
  }
  if (JSON.stringify(spec.panel) !== JSON.stringify({ width: 430, height: 680, cornerRadius: 29 })) {
    errors.push('panel geometry does not match the approved contract');
  }
  if (JSON.stringify(spec.settings) !== JSON.stringify({ width: 820, height: 650 })) {
    errors.push('settings geometry does not match the approved contract');
  }

  const ids = spec.themes.map((theme) => theme?.id);
  if (JSON.stringify(ids) !== JSON.stringify(approvedThemeIds)) {
    errors.push('theme ids or ordering do not match the approved contract');
  }

  spec.themes.forEach((theme, index) => {
    for (const key of themeColorKeys) {
      if (!argbPattern.test(theme?.[key] ?? '')) {
        errors.push(`themes[${index}].${key} must be an uppercase ARGB color`);
      }
    }
  });

  return errors;
}

export function renderCss(spec) {
  const lines = [
    '/* Generated from ui/huahai-ui-spec.json. Do not edit by hand. */',
    ':root{',
    `--huahai-panel-width:${spec.panel.width}px;`,
    `--huahai-panel-height:${spec.panel.height}px;`,
    `--huahai-panel-radius:${spec.panel.cornerRadius}px;`,
    `--huahai-settings-width:${spec.settings.width}px;`,
    `--huahai-settings-height:${spec.settings.height}px;`,
    `--huahai-click-duration:${spec.motion.clickDurationMs}ms;`,
    `--huahai-reduced-click-duration:${spec.motion.reducedClickDurationMs}ms;`,
    `--huahai-specular-proximity:${spec.motion.specularProximityPx}px;`,
    '}',
  ];

  for (const theme of spec.themes) {
    lines.push(`[data-huahai-theme="${theme.id}"]{`);
    for (const key of themeColorKeys) {
      lines.push(`--huahai-${toKebabCase(key)}:${toCssColor(theme[key])};`);
    }
    lines.push('}');
  }

  return `${lines.join('\n')}\n`;
}

export function renderWpf(spec) {
  const primary = spec.themes[0];
  const lines = [
    '<ResourceDictionary',
    '    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
    '    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
    '    xmlns:sys="clr-namespace:System;assembly=mscorlib">',
    '  <!-- Generated from ui/huahai-ui-spec.json. Do not edit by hand. -->',
    `  <sys:Double x:Key="HuahaiPanelWidth">${spec.panel.width}</sys:Double>`,
    `  <sys:Double x:Key="HuahaiPanelHeight">${spec.panel.height}</sys:Double>`,
    `  <CornerRadius x:Key="HuahaiPanelCornerRadius">${spec.panel.cornerRadius}</CornerRadius>`,
    `  <sys:Double x:Key="HuahaiSettingsWidth">${spec.settings.width}</sys:Double>`,
    `  <sys:Double x:Key="HuahaiSettingsHeight">${spec.settings.height}</sys:Double>`,
    `  <sys:Int32 x:Key="HuahaiThemeCount">${spec.themes.length}</sys:Int32>`,
    `  <sys:Double x:Key="HuahaiClickDurationMs">${spec.motion.clickDurationMs}</sys:Double>`,
    `  <sys:Double x:Key="HuahaiReducedClickDurationMs">${spec.motion.reducedClickDurationMs}</sys:Double>`,
    `  <sys:Double x:Key="HuahaiSpecularProximity">${spec.motion.specularProximityPx}</sys:Double>`,
  ];

  for (const theme of spec.themes) {
    const prefix = toPascalCase(theme.id);
    for (const key of themeColorKeys) {
      lines.push(`  <Color x:Key="Huahai${prefix}${capitalize(key)}Color">${theme[key]}</Color>`);
    }
  }

  lines.push(
    `  <Color x:Key="HuahaiAccentColor">${primary.accent}</Color>`,
    `  <Color x:Key="HuahaiReflectionColor">${primary.reflection}</Color>`,
    `  <Color x:Key="HuahaiGlassTopColor">${primary.glassTop}</Color>`,
    `  <Color x:Key="HuahaiGlassBottomColor">${primary.glassBottom}</Color>`,
    `  <Color x:Key="HuahaiContentLensColor">${primary.contentLens}</Color>`,
    `  <Color x:Key="HuahaiTextColor">${primary.text}</Color>`,
    `  <Color x:Key="HuahaiMutedTextColor">${primary.muted}</Color>`,
    '  <SolidColorBrush x:Key="HuahaiAccentBrush" Color="{DynamicResource HuahaiAccentColor}" />',
    '  <SolidColorBrush x:Key="HuahaiContentLensBrush" Color="{DynamicResource HuahaiContentLensColor}" />',
    '  <SolidColorBrush x:Key="HuahaiTextBrush" Color="{DynamicResource HuahaiTextColor}" />',
    '  <SolidColorBrush x:Key="HuahaiMutedTextBrush" Color="{DynamicResource HuahaiMutedTextColor}" />',
    '  <LinearGradientBrush x:Key="HuahaiGlassBrush" StartPoint="0,0" EndPoint="1,1">',
    '    <GradientStop Offset="0" Color="{DynamicResource HuahaiGlassTopColor}" />',
    '    <GradientStop Offset="1" Color="{DynamicResource HuahaiGlassBottomColor}" />',
    '  </LinearGradientBrush>',
    '</ResourceDictionary>',
  );

  return `${lines.join('\n')}\n`;
}

function capitalize(value) {
  return `${value[0].toUpperCase()}${value.slice(1)}`;
}

function toCssColor(argb) {
  const alpha = Number.parseInt(argb.slice(1, 3), 16) / 255;
  const red = Number.parseInt(argb.slice(3, 5), 16);
  const green = Number.parseInt(argb.slice(5, 7), 16);
  const blue = Number.parseInt(argb.slice(7, 9), 16);
  return `rgba(${red},${green},${blue},${alpha.toFixed(3)})`;
}

function toKebabCase(value) {
  return value.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
}

function toPascalCase(value) {
  return value.split('-').map(capitalize).join('');
}

function writeIfChanged(filePath, content) {
  if (fs.existsSync(filePath) && fs.readFileSync(filePath, 'utf8') === content) return;
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content, 'utf8');
}

const invokedPath = process.argv[1] ? path.resolve(process.argv[1]) : '';
if (fileURLToPath(import.meta.url) === invokedPath) {
  const specPath = path.resolve('ui/huahai-ui-spec.json');
  const spec = JSON.parse(fs.readFileSync(specPath, 'utf8'));
  const errors = validateSpec(spec);

  if (errors.length > 0) {
    process.stderr.write(`${errors.join('\n')}\n`);
    process.exitCode = 1;
  } else {
    writeIfChanged('ui/generated/huahai-ui-tokens.css', renderCss(spec));
    writeIfChanged('ui/generated/HuahaiUiTokens.xaml', renderWpf(spec));
  }
}
