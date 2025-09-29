import { sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';
import { getBaseUrl, genAccountIds, createAccounts, deposit, withdraw, balance } from './helpers.js';

// 动态构建阶梯：从 START_RPS 开始，每阶段递增 STEP_RPS，共 STEPS 阶，每阶持续 STAGE_DURATION
function buildStages() {
  const start = Number(__ENV.START_RPS || 100);
  const step = Number(__ENV.STEP_RPS || 100);
  const steps = Number(__ENV.STEPS || 6);
  const dur = __ENV.STAGE_DURATION || '30s';
  const stages = [];
  for (let i = 0; i < steps; i++) {
    stages.push({ target: start + step * i, duration: dur });
  }
  return stages;
}

export const options = {
  scenarios: {
    step: {
      executor: 'ramping-arrival-rate',
      startRate: Number(__ENV.START_RPS || 100),
      timeUnit: '1s',
      preAllocatedVUs: Number(__ENV.PRE_VUS || 50),
      maxVUs: Number(__ENV.MAX_VUS || 2000),
      stages: buildStages(),
    },
  },
  thresholds: {
    http_req_failed: [{ threshold: 'rate<0.02', abortOnFail: false }], // 失败率阈值 2%
    http_req_duration: [
      { threshold: 'p(95)<800', abortOnFail: false }, // 95 分位 RT 限制
    ],
  },
  insecureSkipTLSVerify: true,
};

const p95 = new Trend('rt_p95');
const failRate = new Rate('step_fail_rate');

export function setup() {
  const baseUrl = getBaseUrl();
  const accounts = Number(__ENV.ACCOUNTS || 100);
  const ids = genAccountIds(accounts);
  createAccounts(baseUrl, ids, Number(__ENV.INIT_BALANCE || 100000));
  return { baseUrl, ids };
}

export default function (data) {
  const { baseUrl, ids } = data;
  const r = Math.random();
  const id = ids[Math.floor(Math.random() * ids.length)];
  let res;
  if (r < 0.6) {
    res = deposit(baseUrl, id, Number(__ENV.AMOUNT || 1));
  } else if (r < 0.9) {
    res = withdraw(baseUrl, id, Number(__ENV.AMOUNT || 1));
  } else {
    res = balance(baseUrl, id);
  }
  p95.add(res.timings.duration);
  failRate.add(res.status >= 400);
  if (Number(__ENV.SLEEP || 0) > 0) sleep(Number(__ENV.SLEEP));
}

export function teardown() {}

export function handleSummary(data) {
  return {
    'step-summary.json': JSON.stringify(data, null, 2),
  };
}
