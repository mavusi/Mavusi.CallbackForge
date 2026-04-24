# CallbackForge Web API Examples

## 1. Submit a Simple GET Request

POST http://localhost:5000/api/jobs
Content-Type: application/json

{
  "url": "https://jsonplaceholder.typicode.com/posts/1",
  "method": "GET",
  "callbackUrl": "https://webhook.site/your-unique-id"
}

### Response
HTTP/1.1 202 Accepted
Location: /api/jobs/00000000-0000-0000-0000-000000000000

{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "pending",
  "message": "Job has been submitted and will be processed in the background"
}

---

## 2. Submit a POST Request with Body

POST http://localhost:5000/api/jobs
Content-Type: application/json

{
  "url": "https://jsonplaceholder.typicode.com/posts",
  "method": "POST",
  "headers": {
    "Content-Type": "application/json",
    "Authorization": "Bearer your-token-here"
  },
  "body": "{\"title\": \"foo\", \"body\": \"bar\", \"userId\": 1}",
  "timeout": 30,
  "callbackUrl": "https://webhook.site/your-unique-id",
  "callbackHeaders": {
    "X-Webhook-Secret": "secret123"
  }
}

---

## 3. Submit with Idempotency Key

POST http://localhost:5000/api/jobs
Content-Type: application/json

{
  "url": "https://api.example.com/payment",
  "method": "POST",
  "headers": {
    "Content-Type": "application/json"
  },
  "body": "{\"amount\": 100.00, \"currency\": \"USD\"}",
  "idempotencyKey": "payment-user123-order456",
  "callbackUrl": "https://yourapp.com/webhooks/payment-complete"
}

---

## 4. Get Job Status

GET http://localhost:5000/api/jobs/00000000-0000-0000-0000-000000000000

### Response
HTTP/1.1 200 OK

{
  "jobId": "00000000-0000-0000-0000-000000000000",
  "status": "completed",
  "request": {
    "url": "https://jsonplaceholder.typicode.com/posts/1",
    "method": "GET",
    "headers": {},
    "body": null
  },
  "response": {
    "statusCode": 200,
    "headers": {
      "Content-Type": "application/json; charset=utf-8"
    },
    "body": "{\"userId\": 1, \"id\": 1, \"title\": \"...\", \"body\": \"...\"}",
    "duration": 245.5,
    "receivedAt": "2024-01-15T10:30:00Z"
  },
  "callback": {
    "url": "https://webhook.site/your-unique-id",
    "status": "completed",
    "attempts": 1,
    "lastAttemptAt": "2024-01-15T10:30:01Z",
    "failureReason": null
  },
  "attempts": 1,
  "failureReason": null,
  "createdAt": "2024-01-15T10:29:59Z",
  "updatedAt": "2024-01-15T10:30:01Z"
}

---

## Job Status Values

- `pending` - Job submitted, waiting for processing
- `processing` - Job currently being executed
- `completed` - Job successfully completed
- `failed` - Job failed after all retry attempts
- `cancelled` - Job was cancelled

## Callback Status Values

- `pending` - Callback waiting to be sent
- `inprogress` - Callback currently being sent
- `completed` - Callback successfully delivered
- `failed` - Callback failed after all retry attempts

## Testing with cURL

```bash
# Submit a job
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://jsonplaceholder.typicode.com/posts/1",
    "method": "GET",
    "callbackUrl": "https://webhook.site/your-unique-id"
  }'

# Check job status
curl http://localhost:5000/api/jobs/{jobId}
```

## Testing with PowerShell

```powershell
# Submit a job
$body = @{
    url = "https://jsonplaceholder.typicode.com/posts/1"
    method = "GET"
    callbackUrl = "https://webhook.site/your-unique-id"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/jobs" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

$jobId = $response.jobId

# Check job status
Start-Sleep -Seconds 2
Invoke-RestMethod -Uri "http://localhost:5000/api/jobs/$jobId"
```
