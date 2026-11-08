[CmdletBinding()]
param()

# Generates only the combined documentation HTML. No application or database writes.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command ConvertFrom-Markdown -ErrorAction SilentlyContinue)) {
    throw 'PowerShell 7 with ConvertFrom-Markdown is required.'
}

$docsRoot = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$chapterNames = @(
    '01_System_Overview_and_Scope.md',
    '02_Architecture_Setup_and_Security.md',
    '03_Database_Design_and_Data_Dictionary.md',
    '04_Functional_Modules_and_Workflows.md',
    '05_Reporting_and_Printing.md',
    '06_Development_Timeline_and_Gantt_Input.md',
    '07_Requirements_Traceability_and_Test_Plan.md',
    '08_Known_Limitations_and_Future_Work.md',
    '09_Manuscript_Update_and_Defense_Guide.md',
    '10_Source_Evidence_and_Documentation_Handover.md'
)
$chapterTitles = @(
    'System Overview and Scope',
    'Architecture, Setup, and Security',
    'Database Design and Data Dictionary',
    'Functional Modules and Workflows',
    'Reporting and Printing',
    'Development Timeline and Gantt Input',
    'Requirements Traceability and Test Plan',
    'Known Limitations and Future Work',
    'Manuscript Update and Defense Guide',
    'Source Evidence and Documentation Handover'
)

$sections = [System.Collections.Generic.List[string]]::new()
$contents = [System.Collections.Generic.List[string]]::new()
for ($chapterIndex = 0; $chapterIndex -lt $chapterNames.Count; $chapterIndex++) {
    $chapterName = $chapterNames[$chapterIndex]
    $chapterPath = Join-Path $docsRoot $chapterName
    if (-not (Test-Path -LiteralPath $chapterPath -PathType Leaf)) {
        throw "Required chapter is missing: $chapterName"
    }
    $chapterNumber = '{0:D2}' -f ($chapterIndex + 1)
    $chapterHtml = (ConvertFrom-Markdown -LiteralPath $chapterPath).Html
    # Namespace heading anchors because different chapters reuse numbered headings.
    $chapterHtml = $chapterHtml -replace 'id="([^"]+)"', "id=`"c$chapterNumber-`$1`""
    $chapterHtml = $chapterHtml -replace 'href="#([^"]+)"', "href=`"#c$chapterNumber-`$1`""
    for ($linkIndex = 0; $linkIndex -lt $chapterNames.Count; $linkIndex++) {
        $linkName = [regex]::Escape($chapterNames[$linkIndex])
        $linkNumber = '{0:D2}' -f ($linkIndex + 1)
        $chapterHtml = $chapterHtml -replace "href=`"(?:\./)?$linkName`"", "href=`"#chapter-$linkNumber`""
    }
    $safeTitle = [System.Net.WebUtility]::HtmlEncode($chapterTitles[$chapterIndex])
    $contents.Add("<li><a href=`"#chapter-$chapterNumber`"><span>$chapterNumber</span> $safeTitle</a></li>")
    $sections.Add("<section class=`"chapter`" id=`"chapter-$chapterNumber`"><p class=`"chapter-label`">CHAPTER $chapterNumber</p>$chapterHtml<a class=`"back-link`" href=`"#contents`">Back to contents</a></section>")
}

$style = @'
:root { color-scheme: light; --ink:#1e293b; --muted:#526176; --blue:#1858a8; --line:#d7e1ed; }
* { box-sizing:border-box; }
html { scroll-behavior:smooth; }
body { margin:0; background:#edf2f7; color:var(--ink); font:16px/1.65 "Segoe UI",Arial,sans-serif; }
a { color:var(--blue); text-underline-offset:3px; }
.toolbar { max-width:1120px; margin:20px auto 0; display:flex; gap:14px; align-items:center; justify-content:space-between; padding:0 24px; }
.toolbar button { border:0; border-radius:7px; background:var(--blue); color:white; padding:12px 20px; font:600 15px "Segoe UI",Arial,sans-serif; cursor:pointer; }
.toolbar a { font-size:14px; }
.document { max-width:1120px; margin:20px auto 48px; padding:54px 60px; background:white; box-shadow:0 8px 28px #172b4d10; }
.cover { border-bottom:3px solid var(--blue); padding-bottom:28px; }
.eyebrow,.chapter-label { color:var(--blue); font-size:12px; font-weight:700; letter-spacing:1.8px; }
.cover h1 { font-size:42px; line-height:1.15; margin:16px 0 12px; letter-spacing:-1px; }
.cover .subtitle { font-size:23px; color:var(--muted); margin:0 0 22px; }
.metadata { font-size:14px; color:var(--muted); }
.notice { border-left:4px solid var(--blue); background:#f0f6fd; padding:16px 20px; margin:26px 0; }
.notice p { margin:0; }
#contents { padding:14px 0 30px; }
#contents ol { list-style:none; padding:0; columns:2; column-gap:32px; }
#contents li { margin:0 0 12px; break-inside:avoid; }
#contents li span { display:inline-block; min-width:26px; color:var(--muted); font-size:13px; }
.chapter { padding-top:38px; margin-top:30px; border-top:1px solid var(--line); }
.chapter > h1 { font-size:30px; line-height:1.25; margin:8px 0 24px; }
h2 { font-size:22px; line-height:1.3; margin:32px 0 14px; }
h3 { font-size:18px; line-height:1.35; margin:26px 0 12px; }
h4 { font-size:16px; margin:22px 0 10px; }
p,ul,ol { margin:12px 0 16px; }
li { margin:5px 0; }
table { border-collapse:collapse; width:100%; margin:20px 0 26px; font-size:13px; line-height:1.45; table-layout:auto; }
th,td { border:1px solid var(--line); padding:9px 11px; text-align:left; vertical-align:top; overflow-wrap:anywhere; }
th { background:#eaf1fa; font-weight:700; }
tbody tr:nth-child(even) { background:#fafbfd; }
code { font:0.9em/1.5 Consolas,"Courier New",monospace; background:#f0f3f6; padding:1px 4px; border-radius:3px; overflow-wrap:anywhere; }
pre { padding:15px 18px; background:#f0f3f6; border:1px solid var(--line); white-space:pre-wrap; overflow-wrap:anywhere; }
pre code { padding:0; background:none; }
blockquote { margin:18px 0; padding:8px 18px; border-left:3px solid #aabbd0; color:var(--muted); }
.back-link { display:inline-block; margin-top:24px; font-size:13px; }
.footer { margin-top:40px; padding-top:20px; border-top:1px solid var(--line); color:var(--muted); font-size:13px; }
@media (max-width:760px) { .document { padding:28px 18px; margin:16px 8px; } .cover h1 { font-size:30px; } #contents ol { columns:1; } table { font-size:11px; } th,td { padding:6px; } }
@page { size:A4 portrait; margin:16mm 13mm; }
@media print {
 html { scroll-behavior:auto; }
 body { background:white; color:#111827; font:10pt/1.4 "Segoe UI",Arial,sans-serif; }
 .toolbar,.back-link { display:none!important; }
 .document { max-width:none; margin:0; padding:0; box-shadow:none; }
 .cover h1 { font-size:30pt; }
 .cover .subtitle { font-size:17pt; }
 .metadata { font-size:10pt; }
 .notice { font-size:10pt; padding:9pt 12pt; }
 #contents ol { columns:1; }
 #contents li { margin-bottom:6pt; }
 .chapter { break-before:page; page-break-before:always; border:0; margin:0; padding:0; }
 .chapter > h1 { font-size:22pt; margin:6pt 0 14pt; }
 h2 { font-size:15pt; margin:18pt 0 9pt; }
 h3 { font-size:12pt; margin:14pt 0 8pt; }
 h1,h2,h3,h4,.chapter-label { break-after:avoid; page-break-after:avoid; }
 p,li { orphans:3; widows:3; }
 table { width:100%; font-size:8pt; line-height:1.3; margin:12pt 0 16pt; }
 th,td { padding:5pt; }
 thead { display:table-header-group; }
 tr { break-inside:avoid; page-break-inside:avoid; }
 pre,blockquote { break-inside:avoid; }
 a { color:inherit; text-decoration:none; }
 code { font-size:0.88em; }
 .footer { font-size:8pt; }
}
'@

$html = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="description" content="EZ-K Laundry System source-backed documentation, September 2026 and earlier project history.">
<title>EZ-K Laundry System | Detailed System Documentation</title>
<style>$style</style>
</head>
<body>
<div class="toolbar"><a href="README.md">Editable chapter index</a><button type="button" onclick="window.print()">Print / Save as PDF</button></div>
<main class="document">
<header class="cover">
<p class="eyebrow">PROJECT REFERENCE &amp; DOCUMENTATION HANDOVER</p>
<h1>EZ-K Laundry System</h1>
<p class="subtitle">Detailed System Documentation</p>
<p class="metadata">Database · Customer booking · Staff operations · Owner administration · Reporting<br>Prepared September 30, 2026<br>Coverage: September 2026, with earlier project history</p>
</header>
<aside class="notice"><p><strong>Evidence-first baseline.</strong> Dates from files are labeled as supporting evidence. Unknown actual dates remain to confirm. Implemented source, known limitations, and unexecuted tests are kept separate. Inventory is deferred. This package does not change the application or database.</p></aside>
<nav id="contents" aria-label="Document contents"><h2>Contents</h2><ol>$($contents -join "`n")</ol><p>For your Gantt chart, start with Chapter 06. For your manuscript database section, start with Chapters 03 and 09.</p></nav>
$($sections -join "`n")
<footer class="footer">Documentation baseline: September 30, 2026. The numbered Markdown chapters in this folder are the editable source of truth. Rebuild this reading copy after editing them.</footer>
</main>
</body>
</html>
"@

$outputPath = Join-Path $docsRoot 'EZK_System_Documentation.html'
[System.IO.File]::WriteAllText($outputPath, $html, [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $outputPath ($($chapterNames.Count) chapters)."
