import http from "k6/http";
import { check, sleep } from "k6";

const baseUrl = __ENV.STAYFLOW_BASE_URL || "http://localhost:5000";

export const options = {
  scenarios: {
    health: {
      executor: "constant-arrival-rate",
      rate: 100,
      timeUnit: "1m",
      duration: "1m",
      preAllocatedVUs: 20,
      maxVUs: 60,
      exec: "healthScenario"
    },
    hostWorkspace: {
      executor: "constant-vus",
      vus: 10,
      duration: "1m",
      exec: "hostWorkspaceScenario"
    }
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1200"]
  }
};

export function healthScenario() {
  const response = http.get(`${baseUrl}/health/live`);
  check(response, {
    "health live status 200": (r) => r.status === 200
  });
}

export function hostWorkspaceScenario() {
  const token = __ENV.STAYFLOW_HOST_TOKEN;
  if (!token) {
    sleep(1);
    return;
  }

  const response = http.get(`${baseUrl}/host/copilot/workspace`, {
    headers: {
      Authorization: `Bearer ${token}`
    }
  });

  check(response, {
    "host workspace not 5xx": (r) => r.status < 500
  });
}
