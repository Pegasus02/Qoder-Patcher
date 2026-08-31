import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';

const SALT = Buffer.from('QoderCN-GatewayManager/3.2.0/Mac-Salt', 'utf8');

function getSecretStoreDir() {
  if (process.env.QODER_CN_SECRET_STORE_DIR) {
    return path.resolve(process.env.QODER_CN_SECRET_STORE_DIR);
  }
  const home = os.homedir() || process.env.HOME || '';
  return path.join(home, '.qoder-cn', 'secrets');
}

function deriveKey() {
  const seed = `${os.hostname()}:${os.userInfo().username || ''}:${os.homedir()}:QoderCNPatcherMac`;
  return crypto.pbkdf2Sync(seed, SALT, 100000, 32, 'sha256');
}

function getSecretFilePath(identifier) {
  const norm = (identifier || '').trim().toUpperCase();
  const hash = crypto.createHash('sha256').update(norm, 'utf8').digest('hex');
  return path.join(getSecretStoreDir(), `${hash}.bin`);
}

export const SecretStore = {
  getStoreDirectory() {
    return getSecretStoreDir();
  },

  save(identifier, secret) {
    if (!identifier || !identifier.trim()) {
      throw new Error('Identifier is required.');
    }
    if (!secret || !secret.trim()) {
      this.delete(identifier);
      return;
    }

    const dir = getSecretStoreDir();
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
    }

    const key = deriveKey();
    const iv = crypto.randomBytes(12);
    const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);
    const plaintext = Buffer.from(secret.trim(), 'utf8');
    const ciphertext = Buffer.concat([cipher.update(plaintext), cipher.final()]);
    const tag = cipher.getAuthTag();

    // Format: IV (12 bytes) + TAG (16 bytes) + CIPHERTEXT
    const payload = Buffer.concat([iv, tag, ciphertext]);
    const targetFile = getSecretFilePath(identifier);
    const tempFile = `${targetFile}.tmp-${crypto.randomBytes(4).toString('hex')}`;

    fs.writeFileSync(tempFile, payload, { mode: 0o600 });
    fs.renameSync(tempFile, targetFile);
  },

  load(identifier) {
    if (!identifier || !identifier.trim()) return '';
    const filePath = getSecretFilePath(identifier);
    if (!fs.existsSync(filePath)) return '';

    try {
      const payload = fs.readFileSync(filePath);
      if (payload.length < 28) return ''; // 12 (iv) + 16 (tag) = 28 minimum

      const iv = payload.subarray(0, 12);
      const tag = payload.subarray(12, 28);
      const ciphertext = payload.subarray(28);

      const key = deriveKey();
      const decipher = crypto.createDecipheriv('aes-256-gcm', key, iv);
      decipher.setAuthTag(tag);

      const decrypted = Buffer.concat([decipher.update(ciphertext), decipher.final()]);
      return decrypted.toString('utf8');
    } catch {
      return '';
    }
  },

  delete(identifier) {
    if (!identifier || !identifier.trim()) return;
    const filePath = getSecretFilePath(identifier);
    if (fs.existsSync(filePath)) {
      try {
        fs.unlinkSync(filePath);
      } catch {}
    }
  },

  hasSecret(identifier) {
    if (!identifier || !identifier.trim()) return false;
    const val = this.load(identifier);
    return Boolean(val && val.trim().length > 0);
  },

  saveProviderKey(providerId, apiKey) {
    this.save(`provider:${providerId}`, apiKey);
  },

  loadProviderKey(providerId) {
    return this.load(`provider:${providerId}`);
  },

  deleteProviderKey(providerId) {
    this.delete(`provider:${providerId}`);
  },

  hasProviderKey(providerId) {
    return this.hasSecret(`provider:${providerId}`);
  }
};
