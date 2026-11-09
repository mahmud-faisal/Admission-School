# School Admission System — Take-Home Assignment
| **Stack** | .NET 8+ · PostgreSQL · React 18+ · Docker |


---

## 1. Assignment: School Admission System

### 1.1 Scenario

A private school will take admissions online for the new academic year. Admission at this school is **"first come, first served"** — there is no lottery. Every shift of every class has limited seats (e.g. 120 in "Class 6 – Morning Shift"). As soon as admission opens, thousands of parents apply at the same time.

When someone applies, a seat is **held** in the student's name for a limited time. If the admission fee is paid within that time, the admission is **confirmed**. If the fee is not paid in time, the seat is released again for others.

You must build both the backend and a React UI for this admission system. The school admin and parents will do everything from the UI.

> The brief does not spell out everything. Where it doesn't, assume whatever seems reasonable to you, and write it down with your reasons in `ASSUMPTIONS.md`.

### 1.2 Technology

- **Backend:** C# (.NET 8 or newer) and PostgreSQL. ORM of your choice.
- **Frontend:** React 18 or newer, JavaScript or TypeScript. Starting with Vite is recommended. UI library of your choice.
- The backend runs at `http://localhost:8080`. The database connection must be changeable via an environment variable.
- The backend URL used by the frontend must be changeable via an environment variable.
- The whole project runs with Docker. Keep a `docker-compose.yml` at the repository root so that just running `docker compose up` starts PostgreSQL, the backend and the frontend together. Nothing other than Docker should need to be installed on our machine. This is exactly how we will run the app.
- AI tools may be used.

### 1.3 What the UI must have

A class may have multiple shifts. In this system, **each shift is created as a separate "class"** with its own seat count. For example, "Class 6 – Morning Shift" and "Class 6 – Day Shift" are two separate classes.

No login is needed. At the top of the page there will be a field where the student's **birth registration number** can be entered. The UI will use this as the student's identity.

| Screen | Purpose |
|---|---|
| Create class (school admin) | Create a new class with a class name and seat count |
| Class list | For each class: total, held, confirmed and available seats; an **"Apply"** button |
| Application | Status, time remaining until expiry, **"Pay admission fee"** and **"Cancel"** buttons |
| My applications | All applications of this student and the status of each |

Clicking **"Pay admission fee"** should be treated as a successful payment. No real payment gateway is needed.

The UI doesn't need to look beautiful. What matters is that it works correctly and is easy to use.

### 1.4 API

> The UI will call exactly these APIs. **The contract must be followed exactly**, because we will run automated tests directly against the API as well as the UI. You may add extra fields to responses, but you may **not remove or rename** any of the fields below.

Base URL is `http://localhost:8080`; all requests and responses are JSON. The student's identity comes via the `X-Student-Id` header, whose value is the student's **17-digit birth registration number**. In the API too, each shift is a separate "class" (see §1.3). Example IDs (`c_123`, `h_789`) are only examples — the ID format is up to you.

| # | Method & Path | Purpose |
|---|---|---|
| 1 | `POST /classes` | Create a new class |
| 2 | `GET /classes` | List all classes, with seat counts |
| 3 | `GET /classes/{classId}` | Seat status of one class |
| 4 | `POST /classes/{classId}/holds` | Apply (seat hold) |
| 5 | `GET /holds/{holdId}` | Current status of an application |
| 6 | `POST /holds/{holdId}/confirm` | Pay admission fee, confirm admission |
| 7 | `DELETE /holds/{holdId}` | Cancel application |
| 8 | `GET /me/holds` | All applications of this student |

#### 1. `POST /classes`

```jsonc
// request
{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }

// 201 response
{ "id": "c_123", "name": "Class 6 - Morning Shift", "totalSeats": 120 }
```

#### 2. `GET /classes`

```jsonc
// 200 response
[
  { "id": "c_123", "name": "Class 6 - Morning Shift", "totalSeats": 120, "heldSeats": 15,
    "confirmedSeats": 80, "availableSeats": 25 }
]
```

#### 3. `GET /classes/{classId}`

```jsonc
// 200 response
{ "id": "c_123", "name": "Class 6 - Morning Shift", "totalSeats": 120, "heldSeats": 15,
  "confirmedSeats": 80, "availableSeats": 25 }
```

#### 4. `POST /classes/{classId}/holds`

Header: `X-Student-Id`

```jsonc
// 201 response
{
  "holdId": "h_789",
  "classId": "c_123",
  "studentId": "20152691234567890",
  "status": "HELD",
  "expiresAt": "2026-11-01T04:10:00Z"
}
```

Returns **`409`** if no seat is available.

#### 5. `GET /holds/{holdId}`

```jsonc
// 200 response
{ "holdId": "h_789", "classId": "c_123", "status": "HELD", "expiresAt": "2026-11-01T04:10:00Z" }
```

`status` can be `HELD`, `CONFIRMED`, `EXPIRED` or `CANCELLED`.

#### 6. `POST /holds/{holdId}/confirm`

Header: `X-Student-Id`

```jsonc
// 200 response
{ "holdId": "h_789", "status": "CONFIRMED" }
```

#### 7. `DELETE /holds/{holdId}`

Header: `X-Student-Id`. Returns **`204`** on success.

#### 8. `GET /me/holds`

Header: `X-Student-Id`

```jsonc
// 200 response
[
  { "holdId": "h_789", "classId": "c_123", "className": "Class 6 - Morning Shift", "status":
    "HELD", "expiresAt": "2026-11-01T04:10:00Z" }
]
```

### 1.5 Rules (mandatory)

**Backend**

- Under no circumstances may a class's `held + confirmed` count exceed `totalSeats`, no matter how many requests arrive simultaneously.
- The hold duration is set by the environment variable `HOLD_DURATION_SECONDS` (default `600`). When it expires, the seat must be released automatically — nobody should have to make an API call for it.
- A student may not have more than one active application or confirmed admission in the same class at the same time.
- Only the student's own, non-expired `HELD` application can be confirmed.
- Even if confirm is called multiple times for the same application, `confirmedSeats` increases only once.
- No data is lost when the app restarts, even after `docker compose down` followed by `docker compose up`.

**UI**

- Everything can be done from the UI. Postman or direct DB access should never be needed.
- When a hold expires, the application shows `EXPIRED` without a page reload, and the "Pay admission fee" button is disabled.
- A class's seat counts update every few seconds without a page reload.
- After refreshing the page, or closing and reopening the browser, the student can still see their ongoing applications.
- Any error (seats full, hold expired, invalid input, server down) is shown in language the user can understand. A blank page, or an error only in the console, is not acceptable.

### 1.6 Bonus (optional)

- Even when two backend processes run simultaneously on different ports using the same database, `held + confirmed` must not exceed `totalSeats`. Explain how you ensured this in `ASSUMPTIONS.md`.
- Automated tests for the frontend.

### 1.7 What to submit

- A Git repository, with commit history, containing `backend/` and `frontend/` folders. Commit as you work rather than one big commit at the end.
- A `Dockerfile` for both backend and frontend, and a `docker-compose.yml` at the repository root (see §1.2).
- `README.md`: steps to run the app with Docker, which URL the frontend opens on, and how to run the tests.
- `ASSUMPTIONS.md` (short): what you assumed where the spec isn't clear, and why.
- At least **3 automated tests** for the backend, one of which is a **concurrency test**. A concurrency test means sending many requests at once and showing that no more holds are granted than the number of seats.
- `AI_USAGE.md` (short): which AI tools you used, for which parts, where the AI made mistakes, and how you caught them.

---
