param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [string]$OutputDir = "Docs/EffectInventory"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRootFull = (Resolve-Path $ProjectRoot).Path
$outputFull = Join-Path $projectRootFull $OutputDir
New-Item -ItemType Directory -Force -Path $outputFull | Out-Null

function To-ForwardSlash([string]$path) {
    return ($path -replace "\\", "/")
}

function Get-RelativePath([string]$path) {
    $full = [System.IO.Path]::GetFullPath($path)
    $root = $projectRootFull.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return To-ForwardSlash($full.Substring($root.Length))
    }
    return To-ForwardSlash($full)
}

function Escape-Md([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return "" }
    return ($value -replace '\|', '\|' -replace '\r?\n', ' ')
}

function Size-KB([long]$bytes) {
    return [math]::Round($bytes / 1KB, 1)
}

function Safe-Join([object[]]$items, [string]$separator = "; ") {
    if ($null -eq $items) { return "" }
    return (($items | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Select-Object -Unique) -join $separator)
}

$roots = @(
    [pscustomobject]@{ Package = "Cartoon FX Remaster"; Group = "JMO / Cartoon FX"; Root = "Assets/ThirdParty/JMO Assets/Cartoon FX Remaster"; Notes = "Cartoon prefabs for impacts, elements, text popups, sword trails, and ambient effects." },
    [pscustomobject]@{ Package = "VFX_Klaus"; Group = "Stylized combat VFX"; Root = "Assets/ThirdParty/VFX_Klaus"; Notes = "Stylized particle packs: Hyper Casual, Shoot & Hit, Slash, Element Splash, and timeline demos." },
    [pscustomobject]@{ Package = "Volumetric Fog & Mist 2"; Group = "Kronnect environment"; Root = "Assets/ThirdParty/VolumetricFog2"; Notes = "URP volumetric fog system, fog volumes, fog of war, distant fog, presets, and demos." },
    [pscustomobject]@{ Package = "Volumetric Lights"; Group = "Kronnect environment"; Root = "Assets/ThirdParty/VolumetricLights"; Notes = "URP volumetric lights, dust particles, translucent occlusion, and lighting demo scenes." },
    [pscustomobject]@{ Package = "Volumetric Fog Bundle Archives"; Group = "Imported package archive"; Root = "Assets/ThirdParty/VolumetricFogBundle"; Notes = "Built-in and URP unitypackage archives; imported runtime appears mainly under VolumetricFog2." },
    [pscustomobject]@{ Package = "Volumetric Lights Bundle Archives"; Group = "Imported package archive"; Root = "Assets/ThirdParty/VolumetricLightsBundle"; Notes = "Built-in and URP unitypackage archives; imported runtime appears mainly under VolumetricLights." },
    [pscustomobject]@{ Package = "Project Custom Effects"; Group = "Project-specific"; Root = "Assets/Art/Effect"; Notes = "Project-specific combat effect prefabs." },
    [pscustomobject]@{ Package = "Project Custom Effects"; Group = "Project-specific"; Root = "Assets/Prefabs/Effects"; Notes = "Project-specific effect prefabs." },
    [pscustomobject]@{ Package = "Skill Indicators"; Group = "Project-specific"; Root = "Assets/Art/SkillIndicators"; Notes = "Skill telegraph and targeting indicators: circles, cones, tethers, trajectories, warning zones." },
    [pscustomobject]@{ Package = "Bajiaoshan Wind Frames"; Group = "Project-specific"; Root = "Assets/Art/Weapons/BajiaoshanFrames"; Notes = "Fan wind frame sequence and sprite sheets." },
    [pscustomobject]@{ Package = "Bajiaoshan Animation Assets"; Group = "Project-specific"; Root = "Assets/Art/Weapons/Ani"; Notes = "Fan weapon animation clips/controllers and related highlight materials." },
    [pscustomobject]@{ Package = "Fish Monster Hit Assets"; Group = "Project-specific"; Root = "Assets/Prefabs/Monster/FishAssets/Hit"; Notes = "Fish monster hit feedback texture/audio resources." },
    [pscustomobject]@{ Package = "Dodge Speed Lines"; Group = "Project-specific"; Root = "Assets/Rendering/DodgeSpeedLines"; Notes = "URP screen speed-line post effect with RendererFeature, Volume, and shader." }
)

$rootFullMap = @()
foreach ($root in $roots) {
    $full = Join-Path $projectRootFull ($root.Root -replace "/", [System.IO.Path]::DirectorySeparatorChar)
    if (Test-Path $full) {
        $rootFullMap += [pscustomobject]@{
            Package = $root.Package
            Group = $root.Group
            Root = $root.Root
            RootFull = [System.IO.Path]::GetFullPath($full).TrimEnd("\", "/")
            Notes = $root.Notes
        }
    }
}

$allMeta = Get-ChildItem -Path (Join-Path $projectRootFull "Assets") -Recurse -File -Filter "*.meta" -ErrorAction SilentlyContinue
$guidToAsset = @{}
foreach ($meta in $allMeta) {
    $text = Get-Content -Raw -LiteralPath $meta.FullName -ErrorAction SilentlyContinue
    if ($text -match "(?m)^guid:\s*([0-9a-fA-F]{32})\s*$") {
        $assetPath = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
        $guidToAsset[$Matches[1].ToLowerInvariant()] = Get-RelativePath $assetPath
    }
}

function Get-OwnerInfo([string]$fullPath) {
    $normalized = [System.IO.Path]::GetFullPath($fullPath)
    $match = $rootFullMap |
        Where-Object { $normalized.StartsWith($_.RootFull, [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object { $_.RootFull.Length } -Descending |
        Select-Object -First 1
    if ($null -eq $match) {
        return [pscustomobject]@{ Package = "Keyword Candidate"; Group = "Other"; Root = "Assets"; Notes = "Project asset matched by effect-related keywords." }
    }
    return $match
}

function Get-Subgroup([string]$relativePath, [string]$package) {
    $p = To-ForwardSlash $relativePath
    if ($package -eq "VFX_Klaus") {
        if ($p -match "Prefabs/Hyper Casual FX") { return "Hyper Casual FX" }
        if ($p -match "Prefabs/Stylized Hit & Slash") { return "Stylized Hit & Slash" }
        if ($p -match "Prefabs/Stylized Shoot & Hit Vol\.2") { return "Stylized Shoot & Hit Vol.2" }
        if ($p -match "Prefabs/Stylized Shoot & Hit") { return "Stylized Shoot & Hit" }
        if ($p -match "Prefabs/Stylized_Element_Splash_vol\.1") { return "Stylized Element Splash Vol.1" }
        if ($p -match "Prefabs/Stylized_Element_Splash_vol\.2") { return "Stylized Element Splash Vol.2" }
        if ($p -match "Prefabs/Stylized_Element_Splash_vol\.3") { return "Stylized Element Splash Vol.3" }
        if ($p -match "/Timeline/") { return "Timeline demo prefabs" }
        if ($p -match "/VFX_Lab/") { return "VFX Lab scenes" }
        if ($p -match "/Shaders/") { return "Shaders" }
        if ($p -match "/Materials/") { return "Materials" }
        if ($p -match "/Textures/") { return "Textures" }
    }
    if ($package -eq "Cartoon FX Remaster") {
        if ($p -match "CFXR Prefabs/([^/]+)") { return "CFXR Prefabs / " + $Matches[1] }
        if ($p -match "CFXR Assets/([^/]+)") { return "CFXR Assets / " + $Matches[1] }
        if ($p -match "Demo Assets") { return "Demo Assets" }
    }
    if ($package -eq "Volumetric Fog & Mist 2") {
        if ($p -match "/Resources/") { return "Runtime resources" }
        if ($p -match "/Demo/([^/]+)") { return "Demo / " + $Matches[1] }
        if ($p -match "/Scripts/") { return "Scripts" }
        if ($p -match "/Editor/") { return "Editor tooling" }
    }
    if ($package -eq "Volumetric Lights") {
        if ($p -match "/Resources/") { return "Runtime resources" }
        if ($p -match "/Demos/([^/]+)") { return "Demo / " + $Matches[1] }
        if ($p -match "/Scripts/") { return "Scripts" }
        if ($p -match "/Editor/") { return "Editor tooling" }
    }
    if ($package -eq "Skill Indicators") {
        if ($p -match "/Prefabs/") { return "Indicator prefabs" }
        if ($p -match "/Materials/") { return "Indicator materials" }
        if ($p -match "/Textures/") { return "Indicator textures" }
    }
    return $package
}

function Get-AssetKind([string]$extension) {
    switch ($extension.ToLowerInvariant()) {
        ".prefab" { "Prefab" }
        ".mat" { "Material" }
        ".shader" { "Shader" }
        ".shadergraph" { "Shader Graph" }
        ".png" { "Texture" }
        ".jpg" { "Texture" }
        ".jpeg" { "Texture" }
        ".tga" { "Texture" }
        ".tif" { "Texture" }
        ".unity" { "Demo Scene" }
        ".playable" { "Timeline/Playable" }
        ".anim" { "Animation Clip" }
        ".controller" { "Animator Controller" }
        ".asset" { "Unity Asset" }
        ".unitypackage" { "UnityPackage Archive" }
        ".txt" { "Readme/Text" }
        ".md" { "Readme/Markdown" }
        ".json" { "Data/Manifest" }
        ".wav" { "Audio" }
        default { $extension.TrimStart(".").ToUpperInvariant() }
    }
}

function Get-TagsAndCategory([string]$relativePath, [string]$assetKind) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($relativePath)
    $lower = (To-ForwardSlash $relativePath).ToLowerInvariant()
    $tags = New-Object System.Collections.Generic.List[string]

    $checks = [ordered]@{
        "muzzle" = "Muzzle"
        "projectile|shoot|bullet|arrow|kunai|shuriken|dagger|card|laser|energyball|bomb|hammer|axe" = "Projectile"
        "hit|impact|floorhit|ground hit|wham|pow" = "Hit/Impact"
        "slash|sword|trail|blade" = "Slash/Trail"
        "splash" = "Splash"
        "explosion|firework|boom" = "Explosion"
        "fire|lava|ember|sun|flame" = "Fire"
        "ice|frozen|crystal" = "Ice/Crystal"
        "electric|thunder|spark|lightning" = "Electric"
        "poison|gas|toxic" = "Poison/Gas"
        "water|bubble|liquid|blood" = "Liquid/Blood"
        "wind|dust|smoke|fog|mist|cloud" = "Air/Dust/Fog"
        "leaf|leaves|nature|thorn|plant" = "Nature"
        "magic|aura|circle|portal|energy|orb|shine|glow|buff|charge" = "Magic/Energy"
        "shield|protect" = "Shield"
        "indicator|reticle|telegraph|danger|target|tether|trajectory|cone|zone|footprint|placement|countdown" = "Indicator"
        "rain" = "Weather"
        "text|boing|cursed|wow|poisoned|slash_" = "Comic Text"
        "volumetric|distantfog|fogofwar|fogvolume" = "Volumetric System"
        "speedlines|dodge" = "Screen/Post FX"
        "bajiaoshan|fanwind" = "Weapon Sprite Frames"
        "fish" = "Monster Feedback"
    }
    foreach ($pattern in $checks.Keys) {
        if ($lower -match $pattern) { $tags.Add($checks[$pattern]) }
    }

    $placement = ""
    if ($lower -match "(^|[_\s/(-])air([_\s/)-]|$)") { $placement = "Air/Attach" }
    elseif ($lower -match "floor|ground|circle|zone|reticle|footprint|placement") { $placement = "Ground/Area" }
    elseif ($lower -match "loop|looping|ambient|aura") { $placement = "Loop/Ambient" }

    $category = "Utility/Resource"
    if ($assetKind -eq "Demo Scene") { $category = "Demo Scene" }
    elseif ($assetKind -match "Shader|Material|Texture|Audio|Data|Animation|Animator") { $category = $assetKind }
    elseif ($tags -contains "Indicator") { $category = "Skill Telegraph / Indicator" }
    elseif ($tags -contains "Screen/Post FX") { $category = "Screen / Post FX" }
    elseif ($tags -contains "Volumetric System") { $category = "Environment / Volumetric" }
    elseif ($tags -contains "Shield") { $category = "Shield / Defensive FX" }
    elseif ($tags -contains "Projectile") { $category = "Projectile / Shoot Chain" }
    elseif ($tags -contains "Slash/Trail") { $category = "Melee Slash / Weapon Trail" }
    elseif ($tags -contains "Splash") { $category = "Elemental Splash / Burst" }
    elseif ($tags -contains "Hit/Impact") { $category = "Hit / Impact" }
    elseif ($tags -contains "Explosion") { $category = "Explosion / Burst" }
    elseif ($tags -contains "Magic/Energy") { $category = "Magic / Energy" }
    elseif ($tags -contains "Weapon Sprite Frames") { $category = "Weapon Sprite Frames" }
    elseif ($tags -contains "Monster Feedback") { $category = "Monster Feedback" }
    elseif ($tags.Count -gt 0) { $category = "Elemental / Status FX" }

    return [pscustomobject]@{
        Category = $category
        Tags = Safe-Join $tags
        Placement = $placement
    }
}

function Get-PrefabInfo([string]$fullPath) {
    $text = Get-Content -Raw -LiteralPath $fullPath
    $componentTypes = @()
    foreach ($match in [regex]::Matches($text, "(?m)^--- !u!\d+ &[-0-9]+\s*\r?\n([A-Za-z0-9_]+):")) {
        $componentTypes += $match.Groups[1].Value
    }
    $guids = @()
    foreach ($match in [regex]::Matches($text, "guid:\s*([0-9a-fA-F]{32})")) {
        $guids += $match.Groups[1].Value.ToLowerInvariant()
    }
    $refs = @()
    foreach ($guid in ($guids | Select-Object -Unique)) {
        if ($guidToAsset.ContainsKey($guid)) { $refs += $guidToAsset[$guid] }
    }
    $refExt = @($refs | ForEach-Object { [System.IO.Path]::GetExtension($_).ToLowerInvariant() })
    $componentSummary = ($componentTypes | Group-Object | Sort-Object Count -Descending | ForEach-Object { "$($_.Name):$($_.Count)" }) -join "; "

    return [pscustomobject]@{
        ObjectCount = @($componentTypes | Where-Object { $_ -eq "GameObject" }).Count
        ParticleSystems = @($componentTypes | Where-Object { $_ -eq "ParticleSystem" }).Count
        ParticleRenderers = @($componentTypes | Where-Object { $_ -eq "ParticleSystemRenderer" }).Count
        TrailRenderers = @($componentTypes | Where-Object { $_ -eq "TrailRenderer" }).Count
        LineRenderers = @($componentTypes | Where-Object { $_ -eq "LineRenderer" }).Count
        SpriteRenderers = @($componentTypes | Where-Object { $_ -eq "SpriteRenderer" }).Count
        MeshRenderers = @($componentTypes | Where-Object { $_ -eq "MeshRenderer" }).Count
        Lights = @($componentTypes | Where-Object { $_ -eq "Light" }).Count
        VisualEffects = @($componentTypes | Where-Object { $_ -eq "VisualEffect" }).Count
        MonoBehaviours = @($componentTypes | Where-Object { $_ -eq "MonoBehaviour" }).Count
        PrefabInstances = @($componentTypes | Where-Object { $_ -eq "PrefabInstance" }).Count
        ReferencedMaterials = @($refExt | Where-Object { $_ -eq ".mat" }).Count
        ReferencedTextures = @($refExt | Where-Object { $_ -in @(".png", ".jpg", ".jpeg", ".tga", ".tif") }).Count
        ReferencedScripts = @($refExt | Where-Object { $_ -eq ".cs" }).Count
        ReferencedPrefabs = @($refExt | Where-Object { $_ -eq ".prefab" }).Count
        ReferencedAssets = Safe-Join ($refs | Select-Object -First 12)
        ComponentSummary = $componentSummary
    }
}

function Get-UseHint([string]$category, [string]$tags, [string]$placement, [string]$path) {
    $lower = $path.ToLowerInvariant()
    if ($category -eq "Projectile / Shoot Chain") {
        if ($lower -match "muzzle") { return "Spawn at caster, muzzle, or weapon tip; pair with same-name projectile and hit prefabs." }
        if ($lower -match "projectile") { return "Flying segment; usually needs gameplay code for direction, speed, collision, and hit spawning." }
        if ($lower -match "hit") { return "Impact moment; pair with same-name muzzle/projectile prefabs when possible." }
        return "Complete shoot chain or reusable part; split into muzzle/projectile/hit stages."
    }
    if ($category -eq "Melee Slash / Weapon Trail") { return "Use for melee swings, sword arcs, and weapon trails; layer with hit sparks and speed lines." }
    if ($category -eq "Elemental Splash / Burst") { return "Use for spell bursts and ground impacts; air variants attach to targets, floor variants sit on ground." }
    if ($category -eq "Hit / Impact") { return "General hit feedback; recolor or swap material for different damage elements." }
    if ($category -eq "Explosion / Burst") { return "Explosion or finisher beat; tune scale, camera shake, and audio timing together." }
    if ($category -eq "Magic / Energy") { return "Charging, magic circles, pickup shine, or status loops; loop effects need lifetime control." }
    if ($category -eq "Shield / Defensive FX") { return "Shield/defense feedback; combine with hit sparks, absorption rings, and telegraph indicators." }
    if ($category -eq "Skill Telegraph / Indicator") { return "Show before real damage VFX for targeting, placement, warning, or lock-on clarity." }
    if ($category -eq "Environment / Volumetric") { return "Scene atmosphere system; needs URP RendererFeature/Volume settings." }
    if ($category -eq "Screen / Post FX") { return "Screen-space feedback for dodge, dash, speed burst, or short slow-motion beats." }
    if ($category -eq "Weapon Sprite Frames") { return "Frame sequence or sprite sheet for fan wind, wind blades, UI preview, or SpriteRenderer animation." }
    if ($category -eq "Demo Scene") { return "Open to inspect vendor timing, lighting, camera, and timeline organization." }
    return "Reusable resource component for materials, textures, shaders, data, prefabs, or new VFX assemblies."
}

function Get-CnCategory([string]$category) {
    switch ($category) {
        "Projectile / Shoot Chain" { return "弹道 / 射击链" }
        "Elemental Splash / Burst" { return "元素落点爆发" }
        "Melee Slash / Weapon Trail" { return "近战挥砍 / 武器拖尾" }
        "Elemental / Status FX" { return "元素 / 状态反馈" }
        "Utility/Resource" { return "通用资源 / 组件" }
        "Magic / Energy" { return "魔法 / 能量" }
        "Hit / Impact" { return "命中 / 冲击" }
        "Environment / Volumetric" { return "环境 / 体积系统" }
        "Skill Telegraph / Indicator" { return "技能预警 / 指示器" }
        "Explosion / Burst" { return "爆炸 / 大爆发" }
        "Shield / Defensive FX" { return "护盾 / 防御特效" }
        "Monster Feedback" { return "怪物反馈" }
        "Screen / Post FX" { return "屏幕 / 后处理特效" }
        "Weapon Sprite Frames" { return "武器序列帧" }
        "Demo Scene" { return "演示场景" }
        default { return $category }
    }
}

function Get-CnPlacement([string]$placement) {
    switch ($placement) {
        "Air/Attach" { return "空中 / 挂点" }
        "Ground/Area" { return "地面 / 范围" }
        "Loop/Ambient" { return "循环 / 氛围" }
        default { return $placement }
    }
}

function Get-CnGroupLabel([string]$package, [string]$group) {
    $label = "$package / $group"
    switch ($group) {
        "Hyper Casual FX" { return "$label（通用战斗反馈）" }
        "Stylized Shoot & Hit" { return "$label（射击链 Vol.1）" }
        "Stylized Shoot & Hit Vol.2" { return "$label（射击链 Vol.2）" }
        "Stylized Hit & Slash" { return "$label（命中与挥砍）" }
        "Stylized Element Splash Vol.1" { return "$label（元素爆发 Vol.1）" }
        "Stylized Element Splash Vol.2" { return "$label（元素爆发 Vol.2）" }
        "Stylized Element Splash Vol.3" { return "$label（元素爆发 Vol.3）" }
        "Indicator prefabs" { return "$label（技能预警/指示器）" }
        "Project Custom Effects" { return "$label（项目自用特效）" }
        "Runtime resources" { return "$label（运行时资源）" }
        default { return $label }
    }
}

function Get-CnUseHint([string]$category, [string]$placement, [string]$path) {
    $lower = $path.ToLowerInvariant()
    if ($category -eq "Projectile / Shoot Chain") {
        if ($lower -match "muzzle") { return "放在施法点、枪口或武器尖端，优先和同名 projectile、hit 组成三段链。" }
        if ($lower -match "projectile") { return "作为飞行段使用，通常由玩法脚本控制朝向、速度、碰撞，并在命中时生成 hit。" }
        if ($lower -match "hit") { return "命中/落点瞬间播放，优先和同名 muzzle、projectile 配套。" }
        return "可作为完整射击链或拆件使用，建议按 muzzle、projectile、hit 三段管理。"
    }
    if ($category -eq "Melee Slash / Weapon Trail") { return "适合近战挥砍、剑气、刀光、武器拖尾；可叠加命中火花和屏幕速度线增强打击感。" }
    if ($category -eq "Elemental Splash / Burst") {
        if ($placement -eq "Air/Attach") { return "空中/挂点爆发，适合挂在目标、角色身体、半空命中点。" }
        if ($placement -eq "Ground/Area") { return "地面爆发，适合 AOE 落点、法阵中心、地裂、落雷、冲击波。" }
        return "元素爆发素材，适合技能落点、boss 攻击、属性命中收尾。"
    }
    if ($category -eq "Hit / Impact") { return "通用受击反馈，可按颜色、材质、缩放改成不同属性命中。" }
    if ($category -eq "Explosion / Burst") { return "爆炸或大招收尾，调参时要一起看缩放、屏幕震动、音效时机。" }
    if ($category -eq "Magic / Energy") { return "适合充能、法阵、拾取闪光、状态环绕；循环类需要由生命周期系统管理。" }
    if ($category -eq "Shield / Defensive FX") { return "适合护盾、防御、格挡反馈；可和小型 hit、吸收光圈、持续指示器组合。" }
    if ($category -eq "Skill Telegraph / Indicator") { return "技能前摇/范围预警/锁定/放置提示，通常先显示它，再触发真实伤害特效。" }
    if ($category -eq "Environment / Volumetric") { return "场景氛围系统，适合洞府、森林、神殿光束；使用前检查 URP RendererFeature、Volume、LayerMask。" }
    if ($category -eq "Screen / Post FX") { return "屏幕空间反馈，适合闪避、冲刺、重击瞬间、子弹时间，建议短促触发。" }
    if ($category -eq "Weapon Sprite Frames") { return "序列帧素材，可做扇风、风刃、UI 预览或 SpriteRenderer 动画。" }
    if ($category -eq "Demo Scene") { return "用于观察原厂参数、灯光、相机、Timeline 和摆放方式。" }
    return "可复用的特效组件，适合作为材质、贴图、shader、数据或新 prefab 的拼装零件。"
}

$extensionsToInclude = @(
    ".prefab", ".mat", ".shader", ".shadergraph", ".png", ".jpg", ".jpeg", ".tga", ".tif",
    ".unity", ".playable", ".anim", ".controller", ".asset", ".unitypackage", ".txt", ".md", ".json", ".wav"
)

$fileMap = @{}
foreach ($root in $rootFullMap) {
    Get-ChildItem -Path $root.RootFull -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension.ToLowerInvariant() -in $extensionsToInclude -and $_.Extension.ToLowerInvariant() -ne ".meta" } |
        ForEach-Object { $fileMap[[System.IO.Path]::GetFullPath($_.FullName).ToLowerInvariant()] = $_.FullName }
}

$assetRows = @()
$prefabRows = @()

foreach ($full in ($fileMap.Values | Sort-Object)) {
    $item = Get-Item -LiteralPath $full
    $rel = Get-RelativePath $item.FullName
    $owner = Get-OwnerInfo $item.FullName
    $kind = Get-AssetKind $item.Extension
    $subgroup = Get-Subgroup $rel $owner.Package
    $class = Get-TagsAndCategory $rel $kind
    $hint = Get-UseHint $class.Category $class.Tags $class.Placement $rel

    $assetRows += [pscustomobject]@{
        Package = $owner.Package
        Group = $subgroup
        AssetType = $kind
        Category = $class.Category
        Tags = $class.Tags
        Placement = $class.Placement
        Name = $item.BaseName
        Path = $rel
        SizeKB = Size-KB $item.Length
        UseHint = $hint
    }

    if ($item.Extension.ToLowerInvariant() -eq ".prefab") {
        $info = Get-PrefabInfo $item.FullName
        $prefabRows += [pscustomobject]@{
            Package = $owner.Package
            Group = $subgroup
            Category = $class.Category
            Tags = $class.Tags
            Placement = $class.Placement
            Name = $item.BaseName
            Path = $rel
            GameObjects = $info.ObjectCount
            ParticleSystems = $info.ParticleSystems
            ParticleRenderers = $info.ParticleRenderers
            TrailRenderers = $info.TrailRenderers
            LineRenderers = $info.LineRenderers
            SpriteRenderers = $info.SpriteRenderers
            MeshRenderers = $info.MeshRenderers
            Lights = $info.Lights
            VisualEffects = $info.VisualEffects
            MonoBehaviours = $info.MonoBehaviours
            NestedPrefabs = $info.PrefabInstances
            ReferencedMaterials = $info.ReferencedMaterials
            ReferencedTextures = $info.ReferencedTextures
            ReferencedScripts = $info.ReferencedScripts
            ReferencedPrefabs = $info.ReferencedPrefabs
            ComponentSummary = $info.ComponentSummary
            ReferencedAssetsSample = $info.ReferencedAssets
            UseHint = $hint
        }
    }
}

$packageSummary = $assetRows |
    Group-Object Package, Group |
    ForEach-Object {
        $rows = $_.Group
        $owner = $roots | Where-Object { $_.Package -eq $rows[0].Package } | Select-Object -First 1
        [pscustomobject]@{
            Package = $rows[0].Package
            Group = $rows[0].Group
            Prefabs = @($rows | Where-Object { $_.AssetType -eq "Prefab" }).Count
            Materials = @($rows | Where-Object { $_.AssetType -eq "Material" }).Count
            Textures = @($rows | Where-Object { $_.AssetType -eq "Texture" }).Count
            Shaders = @($rows | Where-Object { $_.AssetType -eq "Shader" }).Count
            ShaderGraphs = @($rows | Where-Object { $_.AssetType -eq "Shader Graph" }).Count
            DemoScenes = @($rows | Where-Object { $_.AssetType -eq "Demo Scene" }).Count
            Animations = @($rows | Where-Object { $_.AssetType -in @("Animation Clip", "Animator Controller") }).Count
            Audio = @($rows | Where-Object { $_.AssetType -eq "Audio" }).Count
            Readmes = @($rows | Where-Object { $_.AssetType -match "Readme" }).Count
            Archives = @($rows | Where-Object { $_.AssetType -eq "UnityPackage Archive" }).Count
            TotalAssets = @($rows).Count
            Notes = if ($owner) { $owner.Notes } else { "" }
        }
    } |
    Sort-Object Package, Group

$categorySummary = $prefabRows |
    Group-Object Category |
    ForEach-Object {
        [pscustomobject]@{
            Category = $_.Name
            Prefabs = $_.Count
            Packages = Safe-Join ($_.Group.Package | Select-Object -Unique)
            ExamplePrefabs = Safe-Join ($_.Group | Select-Object -First 8 -ExpandProperty Name)
        }
    } |
    Sort-Object Category

$assetRows | Sort-Object Package, Group, AssetType, Name | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $outputFull "effect_assets.csv")
$prefabRows | Sort-Object Package, Group, Category, Name | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $outputFull "effect_prefabs.csv")
$packageSummary | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $outputFull "package_summary.csv")
$categorySummary | Export-Csv -NoTypeInformation -Encoding UTF8 -Path (Join-Path $outputFull "category_summary.csv")

$prefabMd = New-Object System.Collections.Generic.List[string]
$prefabMd.Add("# 特效 Prefab 索引")
$prefabMd.Add("")
$prefabMd.Add("由 prefab YAML 和资产元数据生成。组件数量是静态资源计数，不等于运行时发射数量。Prefab 名称和路径保留原始英文，方便在 Unity 里搜索。")
$prefabMd.Add("")
foreach ($group in ($prefabRows | Sort-Object Package, Group, Category, Name | Group-Object Package, Group)) {
    $prefabMd.Add("## " + (Get-CnGroupLabel $group.Group[0].Package $group.Group[0].Group))
    $prefabMd.Add("")
    $prefabMd.Add("| 分类 | 名称 | 放置/空间 | 粒子 | 拖尾 | 线 | 灯光 | 脚本 | 用法建议 | 路径 |")
    $prefabMd.Add("|---|---|---:|---:|---:|---:|---:|---:|---|---|")
    foreach ($row in $group.Group) {
        $lineArgs = @(
            (Escape-Md (Get-CnCategory $row.Category)),
            (Escape-Md $row.Name),
            (Escape-Md (Get-CnPlacement $row.Placement)),
            $row.ParticleSystems,
            $row.TrailRenderers,
            $row.LineRenderers,
            $row.Lights,
            $row.MonoBehaviours,
            (Escape-Md (Get-CnUseHint $row.Category $row.Placement $row.Path)),
            (Escape-Md $row.Path)
        )
        $prefabMd.Add(('| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | `{9}` |' -f $lineArgs))
    }
    $prefabMd.Add("")
}
$prefabMd -join "`n" | Set-Content -Encoding UTF8 -Path (Join-Path $outputFull "effect_prefabs.md")

$report = New-Object System.Collections.Generic.List[string]
$report.Add("# Unity Effect Inventory")
$report.Add("")
$report.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$report.Add("")
$report.Add('Project: `NewFPG` / Unity `6000.3.15f1` / URP `17.3.0`.')
$report.Add("")
$report.Add('This report catalogs imported and project-specific effect resources under `Assets/ThirdParty`, `Assets/Art`, `Assets/Prefabs`, and `Assets/Rendering`. It focuses on reusable VFX prefabs, materials, shaders, textures, demo scenes, sprite sheets, post-process effects, and imported package archives.')
$report.Add("")
$report.Add("## Files")
$report.Add("")
$report.Add("| File | Purpose |")
$report.Add("|---|---|")
$report.Add('| `package_summary.csv` | One row per package/subpackage with asset counts. |')
$report.Add('| `category_summary.csv` | Prefab counts by practical use category. |')
$report.Add('| `effect_assets.csv` | All scanned effect-related assets. |')
$report.Add('| `effect_prefabs.csv` | All scanned prefab assets with component/reference counts. |')
$report.Add('| `effect_prefabs.md` | Human-readable full prefab table. |')
$report.Add('| `Generate-EffectInventory.ps1` | Re-runnable generator. |')
$report.Add("")
$report.Add("## Package Summary")
$report.Add("")
$report.Add("| Package | Group | Prefabs | Materials | Textures | Shaders | ShaderGraphs | Scenes | Anim/Controllers | Audio | Archives | Notes |")
$report.Add("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")
foreach ($row in $packageSummary) {
    $lineArgs = @(
        (Escape-Md $row.Package), (Escape-Md $row.Group), $row.Prefabs, $row.Materials, $row.Textures,
        $row.Shaders, $row.ShaderGraphs, $row.DemoScenes, $row.Animations, $row.Audio, $row.Archives, (Escape-Md $row.Notes)
    )
    $report.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} | {10} | {11} |" -f $lineArgs))
}
$report.Add("")
$report.Add("## Practical Categories")
$report.Add("")
$report.Add("| Category | Prefabs | Main Packages | Examples |")
$report.Add("|---|---:|---|---|")
foreach ($row in $categorySummary) {
    $lineArgs = @((Escape-Md $row.Category), $row.Prefabs, (Escape-Md $row.Packages), (Escape-Md $row.ExamplePrefabs))
    $report.Add(("| {0} | {1} | {2} | {3} |" -f $lineArgs))
}
$report.Add("")
$report.Add("## What Is In The Project")
$report.Add("")
$report.Add("| Area | What it contains | Best use | Assembly notes |")
$report.Add("|---|---|---|---|")
$report.Add("| VFX_Klaus / Hyper Casual FX | Buff up/down, charge steps, dust dash/jump/floor hit, energy, explosions, fire, flashes, hit variants, magic circles, marbles/get effects, poison, portals, shines, splashes, status-style effects. | Fast readable combat feedback, pickups, status changes, dash dust, short burst effects. | Readme says channels/custom data drive dissolve, sharpness, emission, soft particles, and secondary color. Good for material-color variants. |")
$report.Add('| VFX_Klaus / Stylized Shoot & Hit | Named muzzle/projectile/hit chains plus complete `Prefab/FX_Shoot_##` assemblies. | Ranged skills and weapon projectiles. | Keep same-number or same-weapon muzzle/projectile/hit together first, then swap only color/material once timing works. |')
$report.Add('| VFX_Klaus / Shoot & Hit Vol.2 | Arrow, axe, bombs, card, dagger, energy ball, gas, hammer, ice, kunai, lasers, obsidian, poison, shuriken chains. | The cleanest source for modular projectile skills. | Use `_muzzle`, `_projectile`, `_hit` as a three-stage prefab contract. |')
$report.Add('| VFX_Klaus / Stylized Hit & Slash | Slash and hit prefabs for melee contact. | Sword, claw, fan, blade, fish-fin hit feedback. | Pair with CFXR sword trails or custom `FX_splash_sword_new_floor` for bigger melee beats. |')
$report.Add('| VFX_Klaus / Element Splash Vol.1-3 | Air/floor elemental bursts: demon, dust, electric, energy, explosion, force, ice, lava, light, poison, portal, water, wind, crystal, glass, magic, shadow, spark, thunder, blood, bug, coin, meteor, slash, stone, sword, thorn, etc. | Ground AOEs, spell impacts, boss attacks. | `air` versions fit attached/target-space bursts; `floor` versions fit ground decals/rings/impact points. |')
$report.Add("| Cartoon FX Remaster | CFXR categories: Eerie, Electric, Explosions, Fire, Ice, Impacts, Light, Liquids, Magic Misc, Misc, Nature, Sword Trails, Texts. | Plug-and-play cartoon readability, status words, elemental hits, stylized ambience. | Strong for adding character to VFX_Klaus bases: text popups, wind trails, blood/liquid, fire/ice sword trails. |")
$report.Add("| Volumetric Fog & Mist 2 | Fog volumes, sub-volumes, fog-of-war, distant fog, noise textures, presets, URP shaders and demo scenes. | Forest/cave/dongfu atmosphere, conceal/reveal mechanics, mood layers. | Use as scene system, not per-hit particle. Check RendererFeature and volume layer settings before judging visuals. |")
$report.Add("| Volumetric Lights | Volumetric light scripts, dust particles, light shaders, church/temple/minimal demo scenes. | God rays, torch beams, magical shafts, boss arena lighting. | Works best with deliberate lights and occluders; combine with fog for dense atmosphere. |")
$report.Add("| Project custom / Skill Indicators | Ground circles, cones, reticles, line rectangles, tether lines, trajectory arcs, countdown danger, placement ghosts. | Telegraph before attacks, skill placement, tactical clarity. | Use before spawning real damage VFX; color materials already suggest ally/enemy/invalid/valid states. |")
$report.Add("| Project custom / Bajiaoshan | Fan wind sprite frames, sprite sheet, attack animations. | Fan weapon attack visuals and wind slashes. | Combine frame animation with CFXR Wind Trails or VFX_Klaus wind splash floor/air. |")
$report.Add("| Project custom / Dodge Speed Lines | URP RendererFeature, Volume component, shader. | Dash, dodge, speed burst, dodge window feedback. | Screen-space layer: trigger briefly with melee slash/projectile launch to sell velocity. |")
$report.Add("")
$report.Add("## Suggested Assembly Recipes")
$report.Add("")
$report.Add("| Goal | Ingredients | Build order |")
$report.Add("|---|---|---|")
$report.Add('| Sword or fan slash | `Stylized Hit & Slash` slash + CFXR Sword Trail + `FX_splash_sword_new_floor` + Dodge Speed Lines | Start with weapon trail timing, add hit spark at contact, add floor splash for heavy attacks, pulse speed lines for 0.1-0.25s. |')
$report.Add('| Modular projectile skill | Shoot & Hit Vol.2 `_muzzle` + `_projectile` + `_hit` from the same weapon/element | Spawn muzzle at caster, move projectile with gameplay script, spawn hit at collision, then tune scale/colors. |')
$report.Add('| Ground AOE warning into burst | Skill Indicator `PF_IND_GroundCircle`/`PF_IND_Cone` + Element Splash `_floor` | Show indicator during cast, fade/flash at confirm, spawn floor splash at damage frame. |')
$report.Add('| Shield block | CFXR LightGlow or Shield Leaves + small VFX_Klaus hit | Spawn hit sparks on block and add a short emission pulse. |')
$report.Add("| Cave/forest mystic scene | Volumetric Fog2 preset + Volumetric Lights torch/beam + CFXR Ambient Glows/Rain/Wind | Set scene fog first, then light beams, then sparse ambient particles so combat VFX still reads. |")
$report.Add('| Fan wind attack | Bajiaoshan frame animation + CFXR Wind Trails + VFX_Klaus `FX_splash_wind_air/floor` | Use frames for weapon-local wind, air splash for traveling arc, floor splash if it hits terrain/enemy. |')
$report.Add("")
$report.Add("## Usage Notes")
$report.Add("")
$report.Add("- Your project is already URP, which matches VFX_Klaus, Volumetric Fog & Mist 2, Volumetric Lights, and the custom Dodge Speed Lines renderer feature.")
$report.Add("- VFX_Klaus readmes repeatedly mention ParticleSystem Custom Data controlling dissolve, sharpness, distortion, emission, soft particles, and secondary color. When recoloring, inspect Custom Data before duplicating materials.")
$report.Add("- Volumetric Fog/Light are scene-level systems. Treat them as environment layers; do not instantiate them like one-shot combat prefabs unless a gameplay system explicitly manages lifetime and renderer settings.")
$report.Add('- `effect_prefabs.csv` is the fastest file for searching actual usable prefab names. Filter by `Category`, `Tags`, `Placement`, or component counts.')
$report.Add("- The prefab parser is static and conservative. It counts YAML components/references, so nested prefab runtime contents may need opening the source prefab for final tuning.")
$report.Add("")
$report.Add("## Scan Scope")
$report.Add("")
foreach ($root in $roots) {
    $lineArgs = @($root.Root, $root.Notes)
    $report.Add(('- `{0}`: {1}' -f $lineArgs))
}

$report -join "`n" | Set-Content -Encoding UTF8 -Path (Join-Path $outputFull "README.md")

[pscustomobject]@{
    Output = Get-RelativePath $outputFull
    Assets = @($assetRows).Count
    Prefabs = @($prefabRows).Count
    Packages = @($packageSummary | Select-Object -ExpandProperty Package -Unique).Count
    Categories = @($categorySummary).Count
}
