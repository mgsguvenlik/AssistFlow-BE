import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool'
import fs from 'node:fs/promises'

const workbookPath = 'C:/Users/Mehmet Zeki KARA/Downloads/Tahsilat_Musteri_Karar_Listeleri.xlsx'
const contractsPath = 'D:/MGS/Repos/AssistFlow/AssistFlow-BE/.artifact-work/contract-subscription.csv'

const normalizeId = value => {
    const text = String(value ?? '').trim()
    const number = Number(text)
    return Number.isInteger(number) ? String(number) : text
}

const subscriptionByContract = new Map()
for (const line of (await fs.readFile(contractsPath, 'utf8')).replace(/^\uFEFF/, '').split(/\r?\n/)) {
    if (!/^\s*\d+\s*;/.test(line)) continue
    const [contractId, subscriptionStatusId, stageStatus] = line.split(';')
    subscriptionByContract.set(normalizeId(contractId), { subscription: normalizeId(subscriptionStatusId), stage: normalizeId(stageStatus) })
}

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(workbookPath))
const targets = [
    { sheet: '1 - Sözleşme Durumu', contractHeader: 'Legacy Sözleşme No' },
    { sheet: '5 - Null İşlem Türü', contractHeader: 'Legacy Sözleşme No' },
    { sheet: '6 - Tarife Çakışmaları', contractHeader: 'Legacy Sözleşme No' },
    { sheet: '7 - Para Birimi Tutar', contractHeader: 'Legacy Sözleşme No' },
]

const result = []
for (const target of targets) {
    const sheet = workbook.worksheets.getItem(target.sheet)
    const used = sheet.getUsedRange(true)
    const values = used.values
    const headerRowIndex = values.findIndex(row => row.some(cell => String(cell ?? '').trim() === target.contractHeader))
    if (headerRowIndex < 0) throw new Error(`${target.sheet}: başlık satırı bulunamadı`)
    const headers = values[headerRowIndex].map(cell => String(cell ?? '').trim())
    const contractColumn = headers.indexOf(target.contractHeader)
    const rows = values.slice(headerRowIndex + 1).filter(row => row.some(cell => String(cell ?? '').trim() !== ''))
    const counts = new Map()
    const frozenStageCounts = new Map()
    let frozenRows = 0
    let unmatchedRows = 0
    const unmatchedIds = new Set()
    const remainingContractIds = new Set()
    const frozenContractIds = new Set()
    const allContractIds = new Set()
    for (const row of rows) {
        const contractId = normalizeId(row[contractColumn])
        allContractIds.add(contractId)
        const contract = subscriptionByContract.get(contractId)
        const status = contract?.subscription
        counts.set(status ?? '<EŞLEŞMEDİ>', (counts.get(status ?? '<EŞLEŞMEDİ>') ?? 0) + 1)
        if (status === '13') {
            frozenRows++
            frozenContractIds.add(contractId)
            frozenStageCounts.set(contract?.stage ?? '<BOŞ>', (frozenStageCounts.get(contract?.stage ?? '<BOŞ>') ?? 0) + 1)
        } else {
            if (!status) { unmatchedRows++; unmatchedIds.add(contractId) }
            remainingContractIds.add(contractId)
        }
    }
    result.push({
        sheet: target.sheet,
        totalRows: rows.length,
        totalContracts: allContractIds.size,
        frozenRows,
        frozenContracts: frozenContractIds.size,
        remainingRows: rows.length - frozenRows,
        remainingContracts: remainingContractIds.size,
        unmatchedRows,
        unmatchedContractIds: [...unmatchedIds].slice(0, 100),
        subscriptionStatusBreakdown: Object.fromEntries([...counts.entries()].sort()),
        frozenStageBreakdown: Object.fromEntries([...frozenStageCounts.entries()].sort()),
    })
}

console.log(JSON.stringify(result, null, 2))
