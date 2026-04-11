import Modal from './Modal';

export default function ConfirmDialog({ message, onConfirm, onCancel }) {
  return (
    <Modal
      title="⚠️ Xác nhận"
      onClose={onCancel}
      actions={
        <>
          <button className="btn btn-ghost" onClick={onCancel}>Hủy</button>
          <button className="btn btn-primary" onClick={onConfirm}>Xác nhận</button>
        </>
      }
    >
      <p style={{ color: 'var(--text-secondary)', lineHeight: 1.6 }}>{message}</p>
    </Modal>
  );
}
