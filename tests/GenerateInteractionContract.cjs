const fs = require('node:fs');

const panel = 'https://app.huahai.local/Web/product-shell.html#panel';
const settings = page => `https://app.huahai.local/Web/product-shell.html#settings/${page}`;

const definitions = [
  ['panel.search', 'panel', '搜索剪贴板历史', '输入关键字后只显示匹配记录', 'text-input', 'visible-items-filtered'],
  ['panel.minimize', 'panel', '隐藏面板但保持后台运行', '面板隐藏且后台监听继续工作', 'click', 'window-hidden-process-alive'],
  ['panel.settings', 'panel', '进入设置中心', '设置界面打开并可返回面板', 'click', 'settings-visible'],
  ['panel.update-later', 'panel', '暂缓当前版本更新提醒', '通知条收起且齿轮更新红点继续显示', 'click', 'update-reminder-snoozed'],
  ['panel.update-install', 'panel', '从主面板开始安全更新', '关于与更新页面打开并开始经过校验的安装流程', 'click', 'update-install-started'],
  ['panel.summon', 'panel', '从网页预览的隐藏状态重新唤出面板', '面板恢复显示并聚焦搜索框', 'click', 'panel-visible'],
  ['filter.all', 'panel', '查看全部记录', '列表恢复显示全部类型', 'click', 'list-filtered'],
  ['filter.text', 'panel', '只查看文本记录', '列表只显示文本记录', 'click', 'list-filtered'],
  ['filter.link', 'panel', '只查看链接记录', '列表只显示链接记录', 'click', 'list-filtered'],
  ['filter.image', 'panel', '只查看图片记录', '列表只显示图片记录', 'click', 'list-filtered'],
  ['filter.file', 'panel', '只查看文件记录', '列表只显示文件记录', 'click', 'list-filtered'],
  ['filter.favorites', 'panel', '只查看收藏记录', '列表只显示已收藏记录', 'click', 'list-filtered'],
  ['records.scroll', 'panel', '滚动浏览较多历史', '记录列表滚动且行内按钮仍可操作', 'mouse-wheel', 'virtualized-list-scrolls'],
  ['record.copy', 'panel', '复制选中记录', '系统剪贴板更新并按设置立即隐藏面板', 'click', 'clipboard-updated-window-hidden'],
  ['record.pin', 'panel', '置顶或取消置顶记录', '置顶状态保存并重新排序', 'click', 'pin-state-persisted'],
  ['record.favorite', 'panel', '收藏或取消收藏记录', '收藏状态保存并可用收藏筛选查看', 'click', 'favorite-state-persisted'],
  ['record.delete', 'panel', '删除一条历史记录', '目标记录从历史中移除', 'click', 'record-removed'],
  ['panel.autohide', 'panel', '控制复制后是否隐藏', '自动隐藏偏好立即保存', 'toggle', 'autohide-state-updated'],
  ['panel.drag', 'panel', '拖动面板到需要的位置', '窗口位置更新并按显示器保存', 'pointer-drag', 'window-position-updated'],

  ['settings.nav.appearance', 'nav', '打开外观与主题设置', '外观与主题页面显示', 'click', 'settings-page-visible', 'appearance'],
  ['settings.nav.motion', 'nav', '打开动效设置', '动效页面显示', 'click', 'settings-page-visible', 'motion'],
  ['settings.nav.input', 'nav', '打开唤出与隐私设置', '唤出与隐私页面显示', 'click', 'settings-page-visible', 'input'],
  ['settings.nav.storage', 'nav', '打开本机存储设置', '本机存储页面显示', 'click', 'settings-page-visible', 'storage'],
  ['settings.nav.system', 'nav', '打开系统设置', '系统页面显示', 'click', 'settings-page-visible', 'system'],
  ['settings.nav.about', 'nav', '打开关于与更新设置', '关于与更新页面显示', 'click', 'settings-page-visible', 'about'],
  ['settings.back', 'nav', '从设置返回剪贴板面板', '设置关闭并恢复面板尺寸', 'click', 'settings-visible', 'appearance'],
  ['settings.home', 'nav', '点击狐狸图标从任意设置页返回主面板', '设置关闭并恢复主面板尺寸', 'click', 'panel-visible', 'appearance'],

  ['theme.rose', 'appearance', '切换玫瑰紫主题', '玫瑰紫主题立即应用并保存', 'click', 'theme-applied'],
  ['theme.cobalt', 'appearance', '切换钴蓝主题', '钴蓝主题立即应用并保存', 'click', 'theme-applied'],
  ['theme.emerald', 'appearance', '切换翡翠青主题', '翡翠青主题立即应用并保存', 'click', 'theme-applied'],
  ['theme.amber', 'appearance', '切换琥珀橙主题', '琥珀橙主题立即应用并保存', 'click', 'theme-applied'],
  ['theme.aurora', 'appearance', '切换极光青紫主题', '极光青紫主题立即应用并保存', 'click', 'theme-applied'],
  ['appearance.opacity', 'appearance', '调节液态玻璃透明度', '面板材质透明度实时变化并保存', 'slider', 'material-opacity-updated'],
  ['appearance.scale', 'appearance', '等比例缩放整个面板', '字体图标圆角和间距同步缩放', 'slider', 'panel-scale-updated'],
  ['appearance.reset-scale', 'appearance', '恢复默认面板大小', '面板缩放恢复为百分之百', 'click', 'panel-scale-reset'],
  ['appearance.resize-handle', 'appearance', '拖动右下角手柄等比例缩放面板', '字体图标圆角间距与面板按固定比例同步缩放', 'pointer-drag', 'panel-scale-updated'],

  ['motion.petals', 'motion', '开启或关闭背景花瓣', '花瓣背景层状态立即更新并保存', 'toggle', 'petal-state-updated'],
  ['motion.reduced', 'motion', '减少动态效果', '花瓣和液态反光动画按偏好暂停', 'toggle', 'motion-reduced'],
  ['motion.duration', 'motion', '调整按钮反馈时长', '非复制按钮反馈时长更新并保存', 'slider', 'duration-updated'],

  ['input.right-double', 'input', '启用或关闭右键双击唤出', '默认鼠标手势状态更新并保存', 'toggle', 'gesture-state-updated'],
  ['input.capture-shortcut', 'input', '录入自定义键盘或鼠标唤出方式', '输入被规范化显示并自动保存', 'capture-input', 'shortcut-saved'],
  ['input.reset-shortcut', 'input', '清除自定义唤出方式', '恢复仅使用默认右键双击', 'click', 'shortcut-reset'],
  ['input.exclusions', 'input', '编辑不记录剪贴板的应用列表', '排除列表草稿可编辑', 'text-input', 'draft-updated'],
  ['input.save-exclusions', 'input', '保存应用排除列表', '排除列表保存到本机设置', 'click', 'exclusions-saved'],
  ['input.remove-exclusion', 'input', '从应用排除列表移除一项', '目标应用从排除列表移除并立即保存显示状态', 'click', 'exclusion-removed'],

  ['storage.open-folder', 'storage', '打开本机数据目录', '资源管理器打开真实数据目录', 'click', 'folder-opened'],
  ['storage.retention-3', 'storage', '设置普通历史保留三天', '期限保存且收藏和置顶不受清理影响', 'click', 'retention-saved'],
  ['storage.retention-7', 'storage', '设置普通历史保留七天', '期限保存且收藏和置顶不受清理影响', 'click', 'retention-saved'],
  ['storage.retention-30', 'storage', '设置普通历史保留一个月', '期限保存且收藏和置顶不受清理影响', 'click', 'retention-saved'],
  ['storage.count-cleanup-toggle', 'storage', '开启或关闭按普通记录条数自动清理', '开关保存且收藏和置顶不计入上限', 'toggle', 'count-retention-state-saved'],
  ['storage.count-cleanup-limit', 'storage', '设置普通记录自动清理数量上限', '一到一万条的上限保存并立即应用', 'number-input', 'count-retention-limit-saved'],
  ['storage.clear-ordinary', 'storage', '立即清空普通历史', '普通记录删除但收藏和置顶保留', 'click', 'ordinary-history-removed'],
  ['storage.clear-all', 'storage', '确认后清空全部内容', '二次确认后普通收藏和置顶全部删除', 'double-click-confirm', 'all-history-removed'],

  ['system.startup', 'system', '设置开机后台启动', '当前用户开机自启状态更新', 'toggle', 'startup-updated'],
  ['system.background', 'system', '设置关闭面板后继续后台运行', '后台运行偏好更新并保留唤出监听', 'toggle', 'background-updated'],
  ['system.outside-hide', 'system', '设置点击面板外自动隐藏', '窗口失焦时按偏好隐藏且后台监听保持运行', 'toggle', 'outside-hide-updated'],

  ['about.update-toggle', 'about', '设置启动时检查更新', '自动检查偏好保存到本机', 'toggle', 'update-setting-saved'],
  ['about.check-update', 'about', '立即检查 GitHub Release 更新', '显示检查中最新版本可更新或错误状态', 'click', 'update-status-visible'],
  ['about.install-update', 'about', '下载并安装已发现的新版本', '显示下载校验安装进度并启动可回滚安装器', 'click', 'update-install-started'],
  ['about.snooze-update', 'about', '将当前版本更新提醒延后一天', '本版本的主动提醒静默二十四小时但更新状态仍可查看', 'click', 'update-reminder-snoozed'],
  ['about.open-release', 'about', '在浏览器查看公开发布页', '系统浏览器打开项目 Release 页面', 'click', 'browser-opened'],

  ['global.custom-shortcut', 'global', '使用自定义按键在指针位置唤出', '面板在指针位置置顶显示', 'global-input', 'topmost-panel-visible'],
  ['global.right-double-click', 'global', '使用默认右键双击在指针位置唤出', '面板在指针位置置顶显示', 'global-input', 'topmost-panel-visible'],
  ['tray.open-panel', 'tray', '从托盘打开面板', '面板置顶显示', 'menu-click', 'panel-visible'],
  ['tray.open-settings', 'tray', '从托盘进入设置', '设置界面置顶显示', 'menu-click', 'settings-visible'],
  ['tray.exit', 'tray', '从托盘完全退出程序', '监听托盘和后台进程全部退出', 'menu-click', 'process-exited'],
];

const routeFor = (group, page) => {
  if (group === 'panel') return panel;
  if (group === 'nav') return settings('appearance');
  if (['appearance', 'motion', 'input', 'storage', 'system', 'about'].includes(group)) return settings(group);
  if (group === 'global') return 'huahai://background';
  return 'huahai://tray';
};

const sideEffectsFor = group => ({
  panel: ['clipboard', 'encrypted-history', 'local-settings', 'window-visibility'],
  nav: ['window-visibility'],
  appearance: ['local-settings', 'window-geometry'],
  motion: ['local-settings', 'animation-state'],
  input: ['local-settings', 'global-input-registration'],
  storage: ['encrypted-history', 'local-settings', 'system-shell'],
  system: ['local-settings', 'current-user-startup'],
  about: ['local-settings', 'github-release-network', 'system-shell'],
  global: ['global-input', 'window-visibility'],
  tray: ['tray-menu', 'window-visibility', 'process-lifecycle'],
}[group]);

const featureFor = controlId => {
  if (controlId === 'panel.minimize' || controlId === 'panel.summon' || controlId === 'panel.drag' || controlId === 'appearance.resize-handle') return 'panel.window';
  if (controlId === 'panel.settings' || controlId.startsWith('settings.')) return 'settings.navigation';
  if (controlId.startsWith('panel.update-') || controlId.startsWith('about.')) return 'update.lifecycle';
  if (controlId.startsWith('theme.') || controlId.startsWith('appearance.') || controlId.startsWith('motion.')) return 'appearance.customization';
  if (controlId.startsWith('input.') || controlId.startsWith('global.')) return 'input.activation';
  if (controlId.startsWith('storage.')) return 'storage.retention';
  if (controlId.startsWith('system.')) return 'system.behavior';
  if (controlId.startsWith('tray.')) return 'system.tray';
  return 'clipboard.history';
};

const controls = definitions.map(([controlId, group, intent, result, trigger, expected, page]) => {
  const route = routeFor(group, page);
  const isPanel = group === 'panel';
  const isSettings = route.includes('#settings/');
  const control = {
    control_id: controlId,
    test_id: `webview.${controlId}`,
    disposition: 'interactive',
    user_intent: intent,
    fixture: {
      route,
      state: 'isolated-deterministic',
      viewport: isPanel ? { width: 430, height: 680 } : isSettings ? { width: 820, height: 650 } : { width: 430, height: 680 },
    },
    trigger: { type: trigger },
    expected: { type: expected },
    mock_behavior: {
      behavior: 'synthetic',
      observable_result: `${result}；网页预览只改变模拟状态，不读取真实剪贴板或系统设置。`,
    },
    state_contract: {
      loading: '需要等待时显示忙碌状态并阻止重复提交。',
      success: `${result}，界面状态与保存结果保持一致。`,
      error: '失败时保留原状态并显示可读错误信息。',
      disabled: '依赖不可用或当前状态不适用时禁用并说明原因。',
    },
    targets: {
      web: {
        behavior: route.startsWith('huahai://') ? 'simulated-platform-capability' : 'adapted',
        adapter: 'approved product-shell preview adapter'
      },
      desktop: { behavior: 'native', adapter: 'WebView DOM event, C# bridge, and production service' },
    },
    allowed_side_effects: sideEffectsFor(group),
    opens_surface: controlId === 'panel.settings' || controlId === 'tray.open-settings',
    feature_id: featureFor(controlId),
  };
  if (controlId === 'panel.settings' || controlId === 'tray.open-settings') control.exit_control_id = 'settings.back';
  return control;
});

const journeys = controls.map(control => ({
  journey_id: `journey.${control.control_id}`,
  feature_id: control.feature_id,
  user_outcome: control.user_intent,
  fixture: control.fixture,
  steps: [control.control_id],
  expected: control.expected,
  targets: control.targets,
  test_id: `journey.${control.test_id}`,
}));

const contract = {
  version: 2,
  contract_revision: 'huahai-webview-1.1.11-complete-v3',
  evidence_adapters: {
    web: 'assets/playwright/interaction-contract-runner.mjs',
    desktop: 'huahai-native-interaction-runner',
  },
  controls,
  journeys,
};

fs.writeFileSync(
  '.codex/app-product-delivery-interaction-contract.json',
  `${JSON.stringify(contract, null, 2)}\n`,
  'utf8',
);

process.stdout.write(JSON.stringify({ status: 'generated', controls: controls.length, revision: contract.contract_revision }));
