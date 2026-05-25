import { useState } from 'react';
import { Card } from '../components/ui/Card';

/* ─── Flow Data ──────────────────────────────────────────────────────── */

const FLOWS = [
  {
    id: 'link',
    title: '① Liên kết ví',
    subtitle: 'Link Wallet + OTP Verification',
    color: '#6366f1',
    icon: '🔗',
    steps: [
      { id: 'l1', label: 'Chọn Provider', detail: 'Chọn MoMo hoặc VNPay, nhập SĐT ví', actor: 'User', page: '/customers → Wallet btn' },
      { id: 'l2', label: 'Gửi OTP', detail: 'Hệ thống gọi provider, OTP được gửi đến app ví', actor: 'System', api: 'POST /api/customers/{id}/ewallets/link' },
      { id: 'l3', label: 'Nhập OTP', detail: 'Nhập mã OTP 6 số từ app MoMo/VNPay', actor: 'User', page: 'EWallet Modal → OTP field' },
      { id: 'l4', label: 'Xác thực', detail: 'Provider xác nhận OTP, trả token binding', actor: 'System', api: 'POST /api/customers/{id}/ewallets/verify-otp' },
      { id: 'l5', label: 'Ví Active ✅', detail: 'Ví sẵn sàng cho thanh toán / giải ngân', actor: 'System', status: 'success' },
    ]
  },
  {
    id: 'pay',
    title: '② Thanh toán khoản vay',
    subtitle: 'Pay from E-Wallet (Idempotent)',
    color: '#10b981',
    icon: '💳',
    steps: [
      { id: 'p1', label: 'Chọn khoản vay', detail: 'Mở chi tiết khoản vay đang Active/Overdue', actor: 'User', page: '/loans → Chi tiết' },
      { id: 'p2', label: 'Chọn ví & nhập số tiền', detail: 'Chọn ví Active, nhập amount ≤ 100tr VND', actor: 'User', page: 'Pay dialog' },
      { id: 'p3', label: 'Charge ví', detail: 'Trừ tiền từ ví qua Provider API (HMAC signed)', actor: 'System', api: 'POST /api/loans/{id}/pay-from-ewallet' },
      { id: 'p4', label: 'Ghi nhận thanh toán', detail: 'Tự động tạo Payment + LoanTransaction + Allocation', actor: 'System' },
      { id: 'p5', label: 'Hoàn tất ✅', detail: 'Audit log ghi nhận, thông báo gửi đi', actor: 'System', status: 'success' },
    ]
  },
  {
    id: 'disburse',
    title: '③ Giải ngân vào ví',
    subtitle: 'Disburse to E-Wallet (Idempotent)',
    color: '#f59e0b',
    icon: '🏦',
    steps: [
      { id: 'd1', label: 'Khoản vay PendingDisbursement', detail: 'Hồ sơ đã duyệt, chờ giải ngân', actor: 'System', page: '/loans → Status: PendingDisbursement' },
      { id: 'd2', label: 'Chọn ví nhận', detail: 'Chọn ví Active của khách hàng', actor: 'User', page: 'Disbursement dialog' },
      { id: 'd3', label: 'Payout vào ví', detail: 'Chuyển tiền vào ví qua Provider API', actor: 'System', api: 'POST /api/loans/{id}/disburse-to-ewallet' },
      { id: 'd4', label: 'Ghi nhận giải ngân', detail: 'Tạo LoanDisbursement record, chuyển loan → Active', actor: 'System' },
      { id: 'd5', label: 'Loan Active ✅', detail: 'Khoản vay bắt đầu tính lãi, lịch trả nợ tạo', actor: 'System', status: 'success' },
    ]
  },
  {
    id: 'unlink',
    title: '④ Hủy liên kết',
    subtitle: 'Unlink Wallet',
    color: '#ef4444',
    icon: '🔓',
    steps: [
      { id: 'u1', label: 'Chọn ví cần hủy', detail: 'Từ danh sách ví đã liên kết', actor: 'User', page: '/customers → Wallet → Unlink' },
      { id: 'u2', label: 'Soft-delete', detail: 'Status → Unlinked, xóa WalletToken', actor: 'System', api: 'DELETE /api/customers/{id}/ewallets/{walletId}' },
      { id: 'u3', label: 'Hoàn tất ✅', detail: 'Ví không còn hiển thị, không thể dùng', actor: 'System', status: 'success' },
    ]
  },
  {
    id: 'query',
    title: '⑤ Tra cứu giao dịch',
    subtitle: 'Query Transaction Status',
    color: '#8b5cf6',
    icon: '🔍',
    steps: [
      { id: 'q1', label: 'Nhập Transaction ID', detail: 'Mã giao dịch từ kết quả thanh toán/giải ngân', actor: 'User', page: 'Query dialog' },
      { id: 'q2', label: 'Gọi Provider', detail: 'Query trạng thái real-time từ MoMo/VNPay', actor: 'System', api: 'GET /api/ewallet/transactions/{txId}?provider=MoMo' },
      { id: 'q3', label: 'Kết quả', detail: 'SUCCESS / FAILED / PENDING + chi tiết', actor: 'System', status: 'info' },
    ]
  },
];

const ROLES = {
  User: { color: '#6366f1', bg: '#eef2ff' },
  System: { color: '#059669', bg: '#ecfdf5' },
};

/* ─── Components ─────────────────────────────────────────────────────── */

function StepNode({ step, index, total, flowColor, isActive, onClick }) {
  const isLast = index === total - 1;
  const roleStyle = ROLES[step.actor] || ROLES.User;

  return (
    <div
      className={`flow-step ${isActive ? 'flow-step--active' : ''}`}
      onClick={onClick}
      style={{ '--fc': flowColor }}
    >
      {/* Connector line */}
      {!isLast && <div className="flow-connector" />}

      {/* Circle */}
      <div className="flow-circle">
        {step.status === 'success' ? '✓' : step.status === 'info' ? '?' : index + 1}
      </div>

      {/* Content */}
      <div className="flow-content">
        <div className="flow-label">{step.label}</div>
        <span className="flow-role" style={{ color: roleStyle.color, background: roleStyle.bg }}>
          {step.actor}
        </span>
      </div>
    </div>
  );
}

function FlowDetail({ step, flowColor }) {
  if (!step) return <div className="flow-detail-empty">← Nhấn vào bước để xem chi tiết</div>;

  return (
    <div className="flow-detail" style={{ '--fc': flowColor }}>
      <h4>{step.label}</h4>
      <p className="flow-detail-desc">{step.detail}</p>

      {step.page && (
        <div className="flow-detail-row">
          <span className="flow-detail-key">📍 Trang</span>
          <code>{step.page}</code>
        </div>
      )}
      {step.api && (
        <div className="flow-detail-row">
          <span className="flow-detail-key">🔌 API</span>
          <code>{step.api}</code>
        </div>
      )}
      {step.actor && (
        <div className="flow-detail-row">
          <span className="flow-detail-key">👤 Actor</span>
          <span style={{ color: ROLES[step.actor]?.color, fontWeight: 600 }}>{step.actor}</span>
        </div>
      )}
    </div>
  );
}

function FlowCard({ flow, expanded, onToggle }) {
  const [activeStep, setActiveStep] = useState(null);

  return (
    <div className={`flowmap-card ${expanded ? 'flowmap-card--open' : ''}`}>
      <button className="flowmap-card-header" onClick={onToggle} style={{ '--fc': flow.color }}>
        <span className="flowmap-card-icon">{flow.icon}</span>
        <div className="flowmap-card-titles">
          <h3>{flow.title}</h3>
          <p>{flow.subtitle}</p>
        </div>
        <span className="flowmap-card-steps">{flow.steps.length} bước</span>
        <span className={`flowmap-chevron ${expanded ? 'flowmap-chevron--open' : ''}`}>▾</span>
      </button>

      {expanded && (
        <div className="flowmap-card-body">
          <div className="flowmap-columns">
            {/* Steps column */}
            <div className="flowmap-steps">
              {flow.steps.map((step, i) => (
                <StepNode
                  key={step.id}
                  step={step}
                  index={i}
                  total={flow.steps.length}
                  flowColor={flow.color}
                  isActive={activeStep?.id === step.id}
                  onClick={() => setActiveStep(step)}
                />
              ))}
            </div>

            {/* Detail panel */}
            <div className="flowmap-detail-panel">
              <FlowDetail step={activeStep} flowColor={flow.color} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ─── Cheat Sheet ────────────────────────────────────────────────────── */

function CheatSheet() {
  return (
    <Card className="flowmap-cheat">
      <h3 style={{ marginBottom: '1rem' }}>📋 Quick Reference — API Endpoints</h3>
      <div className="flowmap-cheat-grid">
        <div className="flowmap-cheat-item">
          <code className="flowmap-method post">POST</code>
          <code>/api/customers/{'{id}'}/ewallets/link</code>
          <span>Liên kết ví (gửi OTP)</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method post">POST</code>
          <code>/api/customers/{'{id}'}/ewallets/verify-otp</code>
          <span>Xác thực OTP</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method delete">DEL</code>
          <code>/api/customers/{'{id}'}/ewallets/{'{walletId}'}</code>
          <span>Hủy liên kết</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method get">GET</code>
          <code>/api/customers/{'{id}'}/ewallets</code>
          <span>Danh sách ví</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method post">POST</code>
          <code>/api/loans/{'{id}'}/pay-from-ewallet</code>
          <span>Thanh toán (Idempotent)</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method post">POST</code>
          <code>/api/loans/{'{id}'}/disburse-to-ewallet</code>
          <span>Giải ngân (Idempotent)</span>
        </div>
        <div className="flowmap-cheat-item">
          <code className="flowmap-method get">GET</code>
          <code>/api/ewallet/transactions/{'{txId}'}?provider=...</code>
          <span>Tra cứu giao dịch</span>
        </div>
      </div>
    </Card>
  );
}

/* ─── Page ───────────────────────────────────────────────────────────── */

export default function EWalletFlowMap() {
  const [expandedId, setExpandedId] = useState('link');

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>🗺️ E-Wallet Flow Map</h2>
          <p className="subtitle">Bản đồ thao tác Ví Điện Tử — MoMo & VNPay</p>
        </div>
      </div>

      {/* Legend */}
      <div className="flowmap-legend">
        <span className="flowmap-legend-item">
          <span className="flowmap-dot" style={{ background: ROLES.User.color }} />
          User thao tác
        </span>
        <span className="flowmap-legend-item">
          <span className="flowmap-dot" style={{ background: ROLES.System.color }} />
          Hệ thống xử lý
        </span>
        <span className="flowmap-legend-item">
          <span className="flowmap-dot" style={{ background: '#f59e0b' }} />
          Giới hạn: 100.000.000 VND / giao dịch
        </span>
      </div>

      {/* Flow Cards */}
      <div className="flowmap-list">
        {FLOWS.map(flow => (
          <FlowCard
            key={flow.id}
            flow={flow}
            expanded={expandedId === flow.id}
            onToggle={() => setExpandedId(expandedId === flow.id ? null : flow.id)}
          />
        ))}
      </div>

      <CheatSheet />
    </div>
  );
}
