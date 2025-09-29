import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';
import { getBaseUrl, genAccountIds, createAccounts, deposit, withdraw, balance } from './helpers.js';

export const options = {
  scenarios: {
    steady: {
      executor: 'constant-arrival-rate',
      // 可通过环境变量 RPS 指定期望到达率
      rate: Number(__ENV.RPS || 100),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: Number(__ENV.PRE_VUS || 50),
      maxVUs: Number(__ENV.MAX_VUS || 500),
    },
  },
  // 阈值（可按需调整）
  thresholds: {
    http_req_failed: [{ threshold: 'rate<0.01', abortOnFail: false }],
    http_req_duration: [{ threshold: 'p(95)<500', abortOnFail: false }],
  },
  insecureSkipTLSVerify: true,
};

const opCounter = new Counter('ops');

export function setup() {
  const baseUrl = getBaseUrl();
  const accounts = Number(__ENV.ACCOUNTS || 50);
  const ids = genAccountIds(accounts);
  createAccounts(baseUrl, ids, Number(__ENV.INIT_BALANCE || 100000));
  return { baseUrl, ids };
}

export default function (data) {
  const { baseUrl, ids } = data;
  // 简单负载混合：60% 存款、30% 取款、10% 查询
  const r = Math.random();
  const id = ids[Math.floor(Math.random() * ids.length)];

  if (r < 0.6) {
    deposit(baseUrl, id, Number(__ENV.AMOUNT || 1));
    opCounter.add(1);
  } else if (r < 0.9) {
    withdraw(baseUrl, id, Number(__ENV.AMOUNT || 1));
    opCounter.add(1);
  } else {
    balance(baseUrl, id);
    opCounter.add(1);
  }

  // 在 constant-arrival-rate 下无需 sleep，保持为 0
  if (Number(__ENV.SLEEP || 0) > 0) sleep(Number(__ENV.SLEEP));
}

export function teardown() {
  // 无需清理，DB 为本地 SQLite
}

export function handleSummary(data) {
  return {
    'steady-summary.json': JSON.stringify(data, null, 2),
  };
}
