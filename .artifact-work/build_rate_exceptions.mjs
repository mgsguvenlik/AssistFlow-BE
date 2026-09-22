import fs from 'node:fs/promises'
import path from 'node:path'
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const repo = 'D:/MGS/Repos/AssistFlow/AssistFlow-BE'
const sourcePath = path.join(repo, 'tools/Collections.Snapshot/snapshots/mgs-inactive-20260919/reports/rate-currency-amount-exceptions.csv')
const outputDir = path.join(repo, 'outputs/tahsilat-tarife-bilgisi-20260920')
const outputPath = path.join(outputDir, 'Tahsilat_Tarife_Para_Birimi_Tutar_Listesi.xlsx')

function parseCsvLine(line) {
    const result = []
    let value = ''
    let quoted = false
    for (let i = 0; i < line.length; i++) {
        const char = line[i]
        if (char === '"') {
            if (quoted && line[i + 1] === '"') { value += '"'; i++ }
            else quoted = !quoted
        } else if (char === ';' && !quoted) { result.push(value); value = '' }
        else value += char
    }
    result.push(value)
    return result
}

const lines = (await fs.readFile(sourcePath, 'utf8')).replace(/^\uFEFF/, '').split(/\r?\n/).filter(Boolean)
const headers = parseCsvLine(lines[0])
const sourceRows = lines.slice(1).map((line) => {
    const values = parseCsvLine(line)
    return Object.fromEntries(headers.map((header, index) => [header, values[index] ?? '']))
})
const issueDescription = (codes) => {
    const values = []
    if (codes.includes('CURRENCY_MAP_MISSING')) values.push('Para birimi belirlenemedi')
    if (codes.includes('AMOUNT_INVALID')) values.push('Tutar bilgisi geçersiz veya okunamıyor')
    return values.join('; ')
}
const displayDate = (value) => {
    if (!value) return ''
    const date = value.slice(0, 10).split('-')
    return date.length === 3 ? date.reverse().join('.') : value
}
const rows = sourceRows.map((row) => {
    const codes = row.IssueCodes.split(',')
    const period = [row.StartMonth, row.StartYear].filter(Boolean).join('/')
    return [
        row.LegacyContractHistoryId,
        row.LegacyContractId.trim(),
        row.LegacyCustomerId,
        row.SubscriberNo,
        row.CustomerName,
        period,
        displayDate(row.EndDate),
        row.RawCurrencyId,
        row.RawAmount,
        row.ProcessType,
        issueDescription(codes),
        '',
        '',
        '',
        '',
        '',
        codes.join(','),
    ]
})
const currencyCount = sourceRows.filter((r) => r.IssueCodes.includes('CURRENCY_MAP_MISSING')).length
const amountCount = sourceRows.filter((r) => r.IssueCodes.includes('AMOUNT_INVALID')).length
const bothCount = sourceRows.filter((r) => r.IssueCodes.includes('CURRENCY_MAP_MISSING') && r.IssueCodes.includes('AMOUNT_INVALID')).length
const rawCurrencyCounts = [...sourceRows.reduce((map, row) => {
    const key = row.RawCurrencyId || '<BOŞ>'
    map.set(key, (map.get(key) ?? 0) + 1)
    return map
}, new Map()).entries()].sort((a, b) => b[1] - a[1])

const workbook = Workbook.create()
const guide = workbook.worksheets.add('Açıklama')
const data = workbook.worksheets.add('Tarife Bilgileri')
guide.showGridLines = false
data.showGridLines = false
guide.tabColor = '#1F4E78'
data.tabColor = '#5B9BD5'

guide.getRange('A1:F1').merge()
guide.getRange('A1').values = [['Tarife para birimi ve tutar listesi']]
guide.getRange('A1:F1').format = { font: { name: 'Arial', size: 16, bold: true }, rowHeight: 28 }
guide.getRange('A2:F2').merge()
guide.getRange('A2').values = [['Para birimi veya tutarı belirlenemeyen legacy tarife kayıtlarının müşteri mutabakatı.']]
guide.getRange('A2:F2').format = { font: { name: 'Arial', size: 10, italic: true, color: '#595959' } }
guide.getRange('A4:B8').values = [
    ['Kayıt grubu', 'Adet'],
    ['İncelenecek tekil tarife kaydı', rows.length],
    ['Para birimi belirlenemeyen', currencyCount],
    ['Tutarı geçersiz veya okunamayan', amountCount],
    ['Her iki sorunu birlikte taşıyan', bothCount],
]
guide.getRange('D4:E4').values = [['Legacy para birimi değeri', 'Adet']]
const currencySummary = rawCurrencyCounts.slice(0, 4).map(([code, count]) => [code, count])
if (currencySummary.length) guide.getRange(`D5:E${currencySummary.length + 4}`).values = currencySummary
guide.getRange('A4:B4').format = guide.getRange('D4:E4').format = {
    fill: '#1F4E78', font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' },
    horizontalAlignment: 'center',
}
guide.getRange('A5:B8').format.font = { name: 'Arial', size: 10 }
guide.getRange(`D5:E${Math.max(5, currencySummary.length + 4)}`).format.font = { name: 'Arial', size: 10 }
guide.getRange('B5:B8').format.numberFormat = '#,##0'
guide.getRange(`E5:E${Math.max(5, currencySummary.length + 4)}`).format.numberFormat = '#,##0'

guide.getRange('A10:F10').merge()
guide.getRange('A10').values = [['Müşteriden beklenen işlem']]
guide.getRange('A10:F10').format = { fill: '#D9EAF7', font: { name: 'Arial', size: 11, bold: true } }
guide.getRange('A11:F16').values = [
    ['1', 'Tarife Bilgileri sayfasındaki kayıtları inceleyin.', '', '', '', ''],
    ['2', 'Doğru para birimini ve doğru dönem tutarını sarı alanlara yazın.', '', '', '', ''],
    ['3', 'Tutar, seçilen ödeme döneminin tamamı için ödenecek rakam olmalıdır.', '', '', '', ''],
    ['4', 'Bilgi tamamlanabiliyorsa Müşteri Kararı alanında Bilgiyi tamamla seçeneğini işaretleyin.', '', '', '', ''],
    ['5', 'Bilgi belirlenemiyorsa Aktarım dışı bırak veya İncelenecek seçeneğini kullanın.', '', '', '', ''],
    ['6', 'Aktarım dışı bırakılan kayıtlar için kısa bir açıklama ekleyin.', '', '', '', ''],
]
for (let row = 11; row <= 16; row++) guide.getRange(`B${row}:F${row}`).merge()
guide.getRange('A11:F16').format = { font: { name: 'Arial', size: 10 }, wrapText: true, verticalAlignment: 'center' }
guide.getRange('A18:F18').merge()
guide.getRange('A18').values = [['Not: Sarı alanlar müşteri tarafından doldurulacaktır. Legacy kaynak ve teknik referans alanlarını değiştirmeyin.']]
guide.getRange('A18:F18').format = { fill: '#FFF2CC', font: { name: 'Arial', size: 10, bold: true }, wrapText: true }
guide.getRange('A:A').format.columnWidth = 45
guide.getRange('B:B').format.columnWidth = 18
guide.getRange('C:C').format.columnWidth = 3
guide.getRange('D:D').format.columnWidth = 32
guide.getRange('E:E').format.columnWidth = 14
guide.getRange('F:F').format.columnWidth = 3
guide.getRange('A11:F16').format.rowHeight = 32

data.getRange('A1:Q1').merge()
data.getRange('A1').values = [['Tarife para birimi ve tutar kayıtları']]
data.getRange('A1:Q1').format = { font: { name: 'Arial', size: 15, bold: true }, rowHeight: 26 }
data.getRange('A2:Q2').merge()
data.getRange('A2').values = [['Sarı sütunları doldurun. Legacy alanlar yalnız doğru kaydın belirlenmesi için gösterilmektedir.']]
data.getRange('A2:Q2').format = { font: { name: 'Arial', size: 10, italic: true, color: '#595959' } }
const tableHeaders = [
    'Legacy Tarife Kayıt No', 'Legacy Sözleşme No', 'Legacy Müşteri No', 'Abone No',
    'Müşteri Adı', 'Başlangıç Dönemi', 'Bitiş Tarihi', 'Legacy Para Birimi Değeri',
    'Legacy Tutar Değeri', 'Legacy İşlem Türü', 'Sorun Açıklaması', 'Müşteri Kararı',
    'Doğru Para Birimi', 'Diğer Para Birimi', 'Doğru Dönem Tutarı', 'Müşteri Açıklaması',
    'Teknik Referans',
]
data.getRange('A4:Q4').values = [tableHeaders]
const lastRow = rows.length + 4
if (rows.length) data.getRange(`A5:Q${lastRow}`).values = rows
const table = data.tables.add(`A4:Q${lastRow}`, true, 'TarifeBilgisiTablosu')
table.style = 'TableStyleMedium2'
table.showFilterButton = true
data.freezePanes.freezeRows(4)
data.freezePanes.freezeColumns(2)
data.getRange(`A4:Q${lastRow}`).format.font = { name: 'Arial', size: 9 }
data.getRange('A4:Q4').format = {
    fill: '#1F4E78', font: { name: 'Arial', size: 9, bold: true, color: '#FFFFFF' },
    wrapText: true, horizontalAlignment: 'center', verticalAlignment: 'center', rowHeight: 44,
}
data.getRange(`L5:P${lastRow}`).format.fill = '#FFF2CC'
data.getRange(`A5:Q${lastRow}`).format.verticalAlignment = 'top'
for (const column of ['E', 'J', 'K', 'P']) data.getRange(`${column}5:${column}${lastRow}`).format.wrapText = true
data.getRange(`L5:L${lastRow}`).dataValidation = {
    rule: { type: 'list', values: ['Bilgiyi tamamla', 'Aktarım dışı bırak', 'İncelenecek'] },
}
data.getRange(`M5:M${lastRow}`).dataValidation = {
    rule: { type: 'list', values: ['TRY', 'USD', 'EUR', 'GBP', 'Diğer'] },
}
data.getRange(`L5:L${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Aktarım dışı bırak', format: { fill: '#F4CCCC', font: { color: '#9C0006', bold: true } },
})
data.getRange(`L5:L${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Bilgiyi tamamla', format: { fill: '#D9EAD3', font: { color: '#274E13', bold: true } },
})
data.getRange(`O5:O${lastRow}`).format.numberFormat = '#,##0.00'
for (const column of ['A', 'B', 'C', 'D', 'H']) data.getRange(`${column}5:${column}${lastRow}`).format.numberFormat = '@'
const widths = [20, 18, 18, 16, 32, 18, 18, 23, 20, 24, 42, 22, 18, 20, 20, 40, 32]
for (let index = 0; index < widths.length; index++) {
    const letter = String.fromCharCode(65 + index)
    data.getRange(`${letter}:${letter}`).format.columnWidth = widths[index]
}

await fs.mkdir(outputDir, { recursive: true })
const guidePreview = await workbook.render({ sheetName: 'Açıklama', range: 'A1:F18', scale: 1.4, format: 'png' })
await fs.writeFile(path.join(outputDir, 'preview-aciklama.png'), new Uint8Array(await guidePreview.arrayBuffer()))
const dataPreview = await workbook.render({ sheetName: 'Tarife Bilgileri', range: 'A1:Q15', scale: 1, format: 'png' })
await fs.writeFile(path.join(outputDir, 'preview-veri.png'), new Uint8Array(await dataPreview.arrayBuffer()))
const check = await workbook.inspect({
    kind: 'table', range: 'Tarife Bilgileri!A1:Q12', include: 'values,formulas',
    tableMaxRows: 12, tableMaxCols: 17,
})
console.log(check.ndjson)
const errors = await workbook.inspect({
    kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',
    options: { useRegex: true, maxResults: 100 }, summary: 'final formula error scan',
})
console.log(errors.ndjson)
const output = await SpreadsheetFile.exportXlsx(workbook)
await output.save(outputPath)
console.log(JSON.stringify({ outputPath, recordCount: rows.length, currencyCount, amountCount, bothCount }))
