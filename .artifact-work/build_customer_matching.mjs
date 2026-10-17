import fs from 'node:fs/promises'
import path from 'node:path'
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const repo = 'D:/MGS/Repos/AssistFlow/AssistFlow-BE'
const snapshot = path.join(repo, 'tools/Collections.Snapshot/snapshots/mgs-inactive-20260919')
const sourceCsv = path.join(snapshot, 'reports/customer-exceptions.csv')
const customerNdjson = path.join(snapshot, 'customers.ndjson')
const outputDir = path.join(repo, 'outputs/tahsilat-musteri-eslestirme-20260920')
const outputPath = path.join(outputDir, 'Tahsilat_Musteri_Eslestirme_Listesi.xlsx')

function parseCsvLine(line) {
    const values = []
    let value = ''
    let quoted = false
    for (let i = 0; i < line.length; i++) {
        const char = line[i]
        if (char === '"') {
            if (quoted && line[i + 1] === '"') {
                value += '"'
                i++
            } else quoted = !quoted
        } else if (char === ';' && !quoted) {
            values.push(value)
            value = ''
        } else value += char
    }
    values.push(value)
    return values
}

const csvLines = (await fs.readFile(sourceCsv, 'utf8')).replace(/^\uFEFF/, '').split(/\r?\n/).filter(Boolean)
const headers = parseCsvLine(csvLines[0])
const sourceRows = csvLines.slice(1).map((line) => {
    const values = parseCsvLine(line)
    return Object.fromEntries(headers.map((header, index) => [header, values[index] ?? '']))
})

const customers = new Map()
for (const line of (await fs.readFile(customerNdjson, 'utf8')).split(/\r?\n/)) {
    if (!line.trim()) continue
    const row = JSON.parse(line)
    customers.set(String(row.CustomerID).trim(), {
        name: row.Name == null ? '' : String(row.Name).trim(),
        subscriberNo: row.SubscriberNo == null ? '' : String(row.SubscriberNo).trim(),
    })
}

const includedCodes = new Set([
    'CUSTOMER_TARGET_MISSING',
    'CUSTOMER_ORPHAN',
    'SUBSCRIBER_SOURCE_DUPLICATE',
    'SUBSCRIBER_BLANK',
])
const descriptions = {
    CUSTOMER_TARGET_MISSING: 'AssistFlow’da aynı abone numarasıyla müşteri bulunamadı',
    CUSTOMER_ORPHAN: 'Sözleşmenin legacy müşteri kaydı bulunamadı',
    SUBSCRIBER_SOURCE_DUPLICATE: 'Aynı abone numarası birden fazla legacy müşteride bulunuyor',
    SUBSCRIBER_BLANK: 'Legacy müşteri kaydında abone numarası boş',
}

const matchingRows = sourceRows
    .map((row) => {
        const codes = row.IssueCodes.split(',').filter((code) => includedCodes.has(code))
        if (!codes.length) return null
        const sourceCustomer = customers.get(String(row.LegacyCustomerId).trim())
        return [
            row.LegacyContractId,
            row.LegacyCustomerId,
            sourceCustomer?.name ?? '',
            row.SubscriberNo || sourceCustomer?.subscriberNo || '',
            row.TargetCustomerId,
            codes.map((code) => descriptions[code]).join('; '),
            '',
            '',
            '',
            '',
            '',
            codes.join(','),
        ]
    })
    .filter(Boolean)

const issueCounts = new Map()
for (const row of matchingRows) {
    for (const code of row[11].split(',')) issueCounts.set(code, (issueCounts.get(code) ?? 0) + 1)
}

const workbook = Workbook.create()
const guide = workbook.worksheets.add('Açıklama')
const data = workbook.worksheets.add('Müşteri Eşleştirme')
guide.showGridLines = false
data.showGridLines = false
guide.tabColor = '#1F4E78'
data.tabColor = '#5B9BD5'

guide.getRange('A1:F1').merge()
guide.getRange('A1').values = [['Tahsilat müşteri eşleştirme listesi']]
guide.getRange('A1:F1').format = {
    font: { name: 'Arial', size: 16, bold: true, color: '#1F1F1F' },
    rowHeight: 28,
}
guide.getRange('A2:F2').merge()
guide.getRange('A2').values = [[
    'Abone numarası boş, tekrarlı veya AssistFlow müşterisi belirlenemeyen legacy sözleşmeler için müşteri mutabakatı.',
]]
guide.getRange('A2:F2').format = { font: { name: 'Arial', size: 10, italic: true, color: '#595959' }, wrapText: true }
guide.getRange('A4:B8').values = [
    ['Kayıt grubu', 'Adet'],
    ['İncelenecek tekil sözleşme', matchingRows.length],
    ['AssistFlow’da müşteri bulunamadı', issueCounts.get('CUSTOMER_TARGET_MISSING') ?? 0],
    ['Legacy müşteri bağlantısı bulunamadı', issueCounts.get('CUSTOMER_ORPHAN') ?? 0],
    ['Tekrarlı abone numarası', issueCounts.get('SUBSCRIBER_SOURCE_DUPLICATE') ?? 0],
]
guide.getRange('D4:E5').values = [
    ['Kayıt grubu', 'Adet'],
    ['Boş abone numarası', issueCounts.get('SUBSCRIBER_BLANK') ?? 0],
]
guide.getRange('A4:B4').format = guide.getRange('D4:E4').format = {
    fill: '#1F4E78',
    font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' },
    horizontalAlignment: 'center',
}
guide.getRange('A5:B8').format.font = { name: 'Arial', size: 10 }
guide.getRange('D5:E5').format.font = { name: 'Arial', size: 10 }
guide.getRange('B5:B8').format.numberFormat = '#,##0'
guide.getRange('E5').format.numberFormat = '#,##0'

guide.getRange('A10:F10').merge()
guide.getRange('A10').values = [['Müşteriden beklenen işlem']]
guide.getRange('A10:F10').format = {
    fill: '#D9EAF7',
    font: { name: 'Arial', size: 11, bold: true, color: '#1F1F1F' },
}
guide.getRange('A11:F16').values = [
    ['1', 'Müşteri Eşleştirme sayfasındaki her kaydı inceleyin.', '', '', '', ''],
    ['2', 'Müşteri Kararı alanından uygun seçeneği belirleyin.', '', '', '', ''],
    ['3', 'Mevcut müşteriyle eşleştirilecekse doğru SubscriberCode veya AssistFlow Müşteri ID bilgisini girin.', '', '', '', ''],
    ['4', 'Yeni müşteri oluşturulacaksa doğru abone numarası ve müşteri adını yazın.', '', '', '', ''],
    ['5', 'Aktarım dışı bırakılacak kayıtlar için kısa bir açıklama ekleyin.', '', '', '', ''],
    ['6', 'Kararsız kaldığınız kayıtları İncelenecek olarak bırakabilirsiniz.', '', '', '', ''],
]
for (let row = 11; row <= 16; row++) guide.getRange(`B${row}:F${row}`).merge()
guide.getRange('A11:F16').format = { font: { name: 'Arial', size: 10 }, wrapText: true, verticalAlignment: 'center' }
guide.getRange('A18:F18').merge()
guide.getRange('A18').values = [[
    'Not: Sarı alanlar müşteri tarafından doldurulacaktır. Teknik referans alanını değiştirmeyin.',
]]
guide.getRange('A18:F18').format = { fill: '#FFF2CC', font: { name: 'Arial', size: 10, bold: true }, wrapText: true }
guide.getRange('A1:F18').format.font.name = 'Arial'
guide.getRange('A1:F18').format.verticalAlignment = 'center'
guide.getRange('A:A').format.columnWidth = 45
guide.getRange('B:B').format.columnWidth = 18
guide.getRange('C:C').format.columnWidth = 3
guide.getRange('D:D').format.columnWidth = 35
guide.getRange('E:E').format.columnWidth = 14
guide.getRange('F:F').format.columnWidth = 3
guide.getRange('A11:F16').format.rowHeight = 32

data.getRange('A1:L1').merge()
data.getRange('A1').values = [['Müşteri eşleştirme kayıtları']]
data.getRange('A1:L1').format = { font: { name: 'Arial', size: 15, bold: true }, rowHeight: 26 }
data.getRange('A2:L2').merge()
data.getRange('A2').values = [[
    'Sarı sütunları doldurun. Sözleşme ve müşteri numaraları yalnız mutabakat amacıyla gösterilmektedir.',
]]
data.getRange('A2:L2').format = { font: { name: 'Arial', size: 10, italic: true, color: '#595959' }, wrapText: true }

const tableHeaders = [
    'Legacy Sözleşme No',
    'Legacy Müşteri No',
    'Legacy Müşteri Adı',
    'Legacy Abone No',
    'Bulunan AssistFlow Müşteri ID',
    'Sorun Açıklaması',
    'Müşteri Kararı',
    'Doğru SubscriberCode',
    'Doğru AssistFlow Müşteri ID',
    'Doğru Müşteri Adı',
    'Müşteri Açıklaması',
    'Teknik Referans',
]
data.getRange('A4:L4').values = [tableHeaders]
if (matchingRows.length) data.getRange(`A5:L${matchingRows.length + 4}`).values = matchingRows
const lastRow = matchingRows.length + 4
const table = data.tables.add(`A4:L${lastRow}`, true, 'MusteriEslestirmeTablosu')
table.style = 'TableStyleMedium2'
table.showFilterButton = true
data.freezePanes.freezeRows(4)
data.freezePanes.freezeColumns(2)
data.getRange(`A4:L${lastRow}`).format.font = { name: 'Arial', size: 9 }
data.getRange('A4:L4').format = {
    fill: '#1F4E78',
    font: { name: 'Arial', size: 9, bold: true, color: '#FFFFFF' },
    wrapText: true,
    horizontalAlignment: 'center',
    verticalAlignment: 'center',
    rowHeight: 42,
}
data.getRange(`G5:K${lastRow}`).format.fill = '#FFF2CC'
data.getRange(`A5:L${lastRow}`).format.verticalAlignment = 'top'
data.getRange(`C5:C${lastRow}`).format.wrapText = true
data.getRange(`F5:F${lastRow}`).format.wrapText = true
data.getRange(`K5:K${lastRow}`).format.wrapText = true
data.getRange(`G5:G${lastRow}`).dataValidation = {
    rule: {
        type: 'list',
        values: ['Mevcut müşteriyle eşleştir', 'Yeni müşteri oluştur', 'Aktarım dışı bırak', 'İncelenecek'],
    },
}
data.getRange(`G5:G${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Aktarım dışı bırak',
    format: { fill: '#F4CCCC', font: { color: '#9C0006', bold: true } },
})
data.getRange(`G5:G${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Mevcut müşteriyle eşleştir',
    format: { fill: '#D9EAD3', font: { color: '#274E13', bold: true } },
})
data.getRange(`G5:G${lastRow}`).conditionalFormats.add('containsText', {
    text: 'Yeni müşteri oluştur',
    format: { fill: '#D9EAF7', font: { color: '#1F4E78', bold: true } },
})
data.getRange('A:A').format.columnWidth = 18
data.getRange('B:B').format.columnWidth = 18
data.getRange('C:C').format.columnWidth = 34
data.getRange('D:D').format.columnWidth = 18
data.getRange('E:E').format.columnWidth = 22
data.getRange('F:F').format.columnWidth = 48
data.getRange('G:G').format.columnWidth = 28
data.getRange('H:H').format.columnWidth = 22
data.getRange('I:I').format.columnWidth = 24
data.getRange('J:J').format.columnWidth = 34
data.getRange('K:K').format.columnWidth = 42
data.getRange('L:L').format.columnWidth = 32
data.getRange(`A5:B${lastRow}`).format.numberFormat = '@'
data.getRange(`D5:D${lastRow}`).format.numberFormat = '@'
data.getRange(`H5:H${lastRow}`).format.numberFormat = '@'

await fs.mkdir(outputDir, { recursive: true })
const previewGuide = await workbook.render({ sheetName: 'Açıklama', range: 'A1:F18', scale: 1.4, format: 'png' })
await fs.writeFile(path.join(outputDir, 'preview-aciklama.png'), new Uint8Array(await previewGuide.arrayBuffer()))
const previewData = await workbook.render({ sheetName: 'Müşteri Eşleştirme', range: 'A1:L15', scale: 1.1, format: 'png' })
await fs.writeFile(path.join(outputDir, 'preview-veri.png'), new Uint8Array(await previewData.arrayBuffer()))

const check = await workbook.inspect({
    kind: 'table',
    range: 'Müşteri Eşleştirme!A1:L12',
    include: 'values,formulas',
    tableMaxRows: 12,
    tableMaxCols: 12,
})
console.log(check.ndjson)
const errors = await workbook.inspect({
    kind: 'match',
    searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',
    options: { useRegex: true, maxResults: 100 },
    summary: 'final formula error scan',
})
console.log(errors.ndjson)

const output = await SpreadsheetFile.exportXlsx(workbook)
await output.save(outputPath)
console.log(JSON.stringify({ outputPath, recordCount: matchingRows.length }))
