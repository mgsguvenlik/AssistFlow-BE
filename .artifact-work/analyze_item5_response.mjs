import fs from 'node:fs/promises'
import crypto from 'node:crypto'
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool'

const path = 'C:/Users/Mehmet Zeki KARA/Downloads/5 - Null İşlem Türü.xlsx'
const bytes = await fs.readFile(path)
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path))
console.log('SHA256', crypto.createHash('sha256').update(bytes).digest('hex'))
console.log((await workbook.inspect({ kind: 'workbook,sheet,table', maxChars: 7000, tableMaxRows: 4, tableMaxCols: 22 })).ndjson)
const sheet = workbook.worksheets.getItemAt(0)
const values = sheet.getUsedRange().values
await fs.writeFile('.tools/item-five-workbook.json', JSON.stringify({ hash: crypto.createHash('sha256').update(bytes).digest('hex'), values }, null, 2))
console.log('First rows:', JSON.stringify(values.slice(0, 9)))
console.log('Rows:', values.length)
const readJson = async file => JSON.parse((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, ''))
const dbPath = '.tools/item-five-review.json'
if (await fs.stat(dbPath).catch(() => null)) {
    const [histories, contracts, issues, siblings, customers] = await readJson(dbPath)
    const [live] = await readJson('.tools/item-five-legacy-review.json')
    const [logic] = await readJson('.tools/item-five-legacy-logic.json')
    console.log('Legacy objects:', logic.map(o => `${o.SchemaName}.${o.name}`))
    for (const obj of logic) {
        const lines = obj.definition.split(/\r?\n/)
        for (let i = 0; i < lines.length; i++) if (/ProcessType/i.test(lines[i]))
            console.log(obj.name, lines.slice(Math.max(0, i - 2), i + 4).join('\n'))
    }
    const trim = v => String(v ?? '').trim()
    const day = v => trim(v).slice(0, 10)
    const header = values[4]
    const fields = [['Legacy Tarihçe No','ContractHistoryID'],['Legacy Sözleşme No','ContractID'],['Legacy Müşteri No','CustomerID'],['Başlangıç Ay','StartingDateMonth'],['Başlangıç Yıl','StartingDateYear'],['Bitiş Tarihi','EndDate'],['Tutar','Amount'],['Para Birimi ID','CurrencyID'],['Ödeme Dönemi ID','PaymentTypeID'],['Legacy İşlem Türü','ProcessType'],['Açıklama','Description']]
    const same = (a,b,key) => key === 'EndDate' ? day(a) === day(b) : key === 'Amount' ? Number(a) === Number(b) : trim(a) === trim(b)
    const rows = values.slice(5).map((row,index) => {
        const record = Object.fromEntries(header.map((h,i) => [h,row[i]]))
        const id = trim(record['Legacy Tarihçe No'])
        const stage = histories.find(h => trim(h.SourceId) === id)
        if (!stage) throw Error(`Kaynak tarihçe bulunamadı: ${id}`)
        const raw = JSON.parse(stage.Payload)
        if (crypto.createHash('sha256').update(stage.Payload).digest('hex').toUpperCase() !== stage.PayloadHash) throw Error(`Hash uyuşmazlığı ${id}`)
        const current = live.find(h => String(h.ContractHistoryID) === id)
        const parent = contracts.find(c => trim(c.SourceId) === trim(stage.SourceContractId))
        const related = siblings.filter(r => trim(r.SourceContractId) === trim(stage.SourceContractId))
        const workbookMismatch = fields.filter(([h,k]) => !same(record[h],raw[k],k)).map(([h]) => h)
        const liveMismatch = !current ? ['MGS kaydı yok'] : fields.filter(([,k]) => !same(raw[k],current[k],k)).map(([,k]) => k)
        const rowIssues = issues.filter(i => i.EntityCode === 'ContractHistory' && i.SourceId === id && i.Status === 0).map(i => i.IssueCode)
        return { row:index+6, historyId:id, contractId:trim(stage.SourceContractId), customerId:trim(stage.SourceCustomerId),
            note:trim(record['TAHSİLAT AÇIKLAMA']), stageStatus:stage.Status, parentStatus:parent?.Status ?? null,
            parentExists:!!parent, currentParentExists:!!current?.ExistingContractID,
            amount:stage.Amount, currencyId:stage.TargetCurrencyTypeId, workbookMismatch, liveMismatch,
            rawProcessType:raw.ProcessType, liveProcessType:current?.ProcessType, rowIssues,
            otherHistoryIssues:[...new Set(related.filter(r => r.SourceId !== id && r.IssueStatus === 0).map(r => r.IssueCode))],
            siblingCount:new Set(related.map(r => r.SourceId)).size,
            priorDecisions:issues.filter(i => i.EntityCode === 'Contract' && i.SourceId === trim(stage.SourceContractId) && i.ResolutionNote).map(i => i.ResolutionNote).filter((s,i,a) => a.indexOf(s) === i)
        }
    })
    if (new Set(rows.map(r => r.historyId)).size !== rows.length) throw Error('Tekrarlı tarihçe kimliği')
    const counts = rows.reduce((a,r) => {a[r.note||'YANIT YOK']=(a[r.note||'YANIT YOK']||0)+1; return a}, {})
    await fs.writeFile('.tools/item-five-comparison.json', JSON.stringify({ workbookHash:crypto.createHash('sha256').update(bytes).digest('hex'),counts,rows },null,2))
    console.log('COMPARISON:',JSON.stringify({counts,rows},null,2))
}
