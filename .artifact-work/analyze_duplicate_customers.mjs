import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool'
import fs from 'node:fs/promises'

const workbookPath = 'C:/Users/Mehmet Zeki KARA/Downloads/Tahsilat_Musteri_Karar_Listeleri.xlsx'
const normalize = value => String(value ?? '').trim()
const normalizeSubscriber = value => normalize(value).toLocaleUpperCase('tr-TR')

const customers = new Map()
const subscriberGroups = new Map()
for (const line of (await fs.readFile('.artifact-work/customer-created.csv', 'latin1')).split(/\r?\n/)) {
    if (!/^\s*\d+\s*\|/.test(line)) continue
    const [idRaw, subscriberRaw, , createdRaw] = line.split('|')
    const id = normalize(idRaw)
    const subscriber = normalizeSubscriber(subscriberRaw)
    const createdText = normalize(createdRaw)
    const createdTime = Date.parse(createdText)
    const customer = { id, subscriber, createdText, createdTime: Number.isFinite(createdTime) ? createdTime : null }
    customers.set(id, customer)
    if (subscriber) {
        if (!subscriberGroups.has(subscriber)) subscriberGroups.set(subscriber, [])
        subscriberGroups.get(subscriber).push(customer)
    }
}

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(workbookPath))
const sheet = workbook.worksheets.getItem('4 - Müşteri Eşleştirme')
const values = sheet.getUsedRange(true).values
const headerIndex = values.findIndex(r => r.some(c => normalize(c) === 'Legacy Sözleşme No'))
const headers = values[headerIndex].map(normalize)
const idx = Object.fromEntries(headers.map((h, i) => [h, i]))
const rows = values.slice(headerIndex + 1).filter(r => r.some(c => normalize(c)))

let duplicateRows = 0
let removedOldRows = 0
let retainedNewestDuplicateRows = 0
let unresolvedDuplicateRows = 0
const removedCustomerIds = new Set()
const retainedCustomerIds = new Set()
const unresolvedCustomerIds = new Set()
const kept = []
const duplicateIssueCombinations = new Map()
let newestOnlyDuplicateRows = 0
let newestWithOtherIssueRows = 0

for (const row of rows) {
    const issueCodes = normalize(row[idx['Teknik Referans']]).split(',').filter(Boolean)
    if (!issueCodes.includes('SUBSCRIBER_SOURCE_DUPLICATE')) {
        kept.push(row)
        continue
    }
    duplicateRows++
    const combination = issueCodes.sort().join(',')
    duplicateIssueCombinations.set(combination, (duplicateIssueCombinations.get(combination) ?? 0) + 1)
    const customerId = normalize(row[idx['Legacy Müşteri No']])
    const customer = customers.get(customerId)
    const subscriber = customer?.subscriber || normalizeSubscriber(row[idx['Legacy Abone No']])
    const group = subscriberGroups.get(subscriber) ?? []
    const dated = group.filter(c => c.createdTime !== null)
    const maxDate = dated.length ? Math.max(...dated.map(c => c.createdTime)) : null
    const newest = maxDate === null ? [] : dated.filter(c => c.createdTime === maxDate)
    if (!customer || customer.createdTime === null || newest.length !== 1) {
        unresolvedDuplicateRows++
        unresolvedCustomerIds.add(customerId)
        kept.push(row)
    } else if (customer.id === newest[0].id) {
        retainedNewestDuplicateRows++
        retainedCustomerIds.add(customerId)
        if (issueCodes.length === 1) newestOnlyDuplicateRows++
        else newestWithOtherIssueRows++
        kept.push(row)
    } else {
        removedOldRows++
        removedCustomerIds.add(customerId)
    }
}

console.log(JSON.stringify({
    originalRows: rows.length,
    duplicateRows,
    removedOldRows,
    removedOldCustomers: removedCustomerIds.size,
    retainedNewestDuplicateRows,
    retainedNewestCustomers: retainedCustomerIds.size,
    unresolvedDuplicateRows,
    unresolvedDuplicateCustomers: unresolvedCustomerIds.size,
    newestOnlyDuplicateRows,
    newestWithOtherIssueRows,
    remainingRows: kept.length,
    duplicateIssueCombinations: Object.fromEntries([...duplicateIssueCombinations.entries()].sort()),
}, null, 2))
