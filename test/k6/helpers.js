import http from 'k6/http';
import { check } from 'k6';

export function getBaseUrl() {
  // 推荐使用 https 端口 7067，并在运行时开启 --insecure-skip-tls-verify
  return __ENV.BASE_URL || 'http://localhost:5145';
}

export function jsonHeaders() {
  return { 'Content-Type': 'application/json' };
}

export function genAccountIds(count, prefix = 'acct') {
  const ids = [];
  const base = Date.now() + '-' + Math.floor(Math.random() * 1e6);
  for (let i = 0; i < count; i++) {
    ids.push(`${prefix}-${base}-${i}`);
  }
  return ids;
}

export function createAccounts(baseUrl, ids, initialBalance = 100000, accountHolderPrefix = 'tester') {
  const payloads = ids.map((id, idx) => ({
    method: 'POST',
    url: `${baseUrl}/api/BankAccount`,
    body: JSON.stringify({
      AccountId: id,
      AccountHolder: `${accountHolderPrefix}-${idx}`,
      InitialBalance: initialBalance,
    }),
    params: { headers: jsonHeaders() },
  }));
  const responses = http.batch(payloads);
  // 允许并发重复运行时已有账号（409）或成功（2xx）
  for (const res of responses) {
    check(res, {
      'setup create account 2xx or 409': (r) => (r.status >= 200 && r.status < 300) || r.status === 409,
    });
  }
  return ids;
}

export function deposit(baseUrl, accountId, amount = 1, description = 'k6 deposit') {
  const res = http.post(
    `${baseUrl}/api/BankAccount/${encodeURIComponent(accountId)}/deposit`,
    JSON.stringify({ Amount: amount, Description: description }),
    { headers: jsonHeaders() },
  );
  check(res, { 'deposit 2xx': (r) => r.status >= 200 && r.status < 300 });
  return res;
}

export function withdraw(baseUrl, accountId, amount = 1, description = 'k6 withdraw') {
  const res = http.post(
    `${baseUrl}/api/BankAccount/${encodeURIComponent(accountId)}/withdraw`,
    JSON.stringify({ Amount: amount, Description: description }),
    { headers: jsonHeaders() },
  );
  // 允许资金不足时报 400
  check(res, { 'withdraw ok or 400': (r) => (r.status >= 200 && r.status < 300) || r.status === 400 });
  return res;
}

export function balance(baseUrl, accountId) {
  const res = http.get(`${baseUrl}/api/BankAccount/${encodeURIComponent(accountId)}/balance`);
  check(res, { 'balance 2xx': (r) => r.status >= 200 && r.status < 300 });
  return res;
}

