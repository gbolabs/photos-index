/**
 * Test data utilities for E2E tests
 * Provides sample data for testing
 */

export interface TestDirectory {
  path: string;
  description: string;
  isEnabled: boolean;
}

export interface TestFile {
  fileName: string;
  filePath: string;
  size: number;
  hash: string;
  createdAt: string;
}

export interface TestDuplicateGroup {
  hash: string;
  files: TestFile[];
  totalSize: number;
}

/**
 * Sample directory configurations for testing
 */
export const testDirectories: TestDirectory[] = [
  {
    path: '/test/photos/vacation',
    description: 'Vacation photos for testing',
    isEnabled: true,
  },
  {
    path: '/test/photos/family',
    description: 'Family photos for testing',
    isEnabled: true,
  },
  {
    path: '/test/photos/work',
    description: 'Work photos for testing',
    isEnabled: false,
  },
];

/**
 * Sample file data for testing
 */
export const testFiles: TestFile[] = [
  {
    fileName: 'photo1.jpg',
    filePath: '/test/photos/vacation/photo1.jpg',
    size: 1024000,
    hash: 'abc123def456',
    createdAt: '2024-01-01T12:00:00Z',
  },
  {
    fileName: 'photo2.jpg',
    filePath: '/test/photos/vacation/photo2.jpg',
    size: 2048000,
    hash: 'def456abc123',
    createdAt: '2024-01-02T12:00:00Z',
  },
  {
    fileName: 'photo3.png',
    filePath: '/test/photos/family/photo3.png',
    size: 512000,
    hash: 'ghi789jkl012',
    createdAt: '2024-01-03T12:00:00Z',
  },
];

/**
 * Sample duplicate groups for testing
 */
export const testDuplicateGroups: TestDuplicateGroup[] = [
  {
    hash: 'duplicate123',
    totalSize: 3072000,
    files: [
      {
        fileName: 'duplicate1.jpg',
        filePath: '/test/photos/vacation/duplicate1.jpg',
        size: 1024000,
        hash: 'duplicate123',
        createdAt: '2024-01-01T12:00:00Z',
      },
      {
        fileName: 'duplicate2.jpg',
        filePath: '/test/photos/family/duplicate2.jpg',
        size: 1024000,
        hash: 'duplicate123',
        createdAt: '2024-01-02T12:00:00Z',
      },
      {
        fileName: 'duplicate3.jpg',
        filePath: '/test/photos/work/duplicate3.jpg',
        size: 1024000,
        hash: 'duplicate123',
        createdAt: '2024-01-03T12:00:00Z',
      },
    ],
  },
];

/**
 * Generate a random test directory
 */
export function generateTestDirectory(index: number = 0): TestDirectory {
  return {
    path: `/test/photos/dir-${Date.now()}-${index}`,
    description: `Test directory ${index}`,
    isEnabled: true,
  };
}

/**
 * Generate a random test file
 */
export function generateTestFile(index: number = 0): TestFile {
  const timestamp = Date.now();
  return {
    fileName: `test-file-${timestamp}-${index}.jpg`,
    filePath: `/test/photos/test-file-${timestamp}-${index}.jpg`,
    size: Math.floor(Math.random() * 5000000) + 100000, // 100KB - 5MB
    hash: `hash-${timestamp}-${index}`,
    createdAt: new Date().toISOString(),
  };
}

/**
 * Generate a duplicate group with multiple files
 */
export function generateTestDuplicateGroup(fileCount: number = 3): TestDuplicateGroup {
  const hash = `duplicate-${Date.now()}`;
  const fileSize = Math.floor(Math.random() * 5000000) + 100000;

  const files: TestFile[] = [];
  for (let i = 0; i < fileCount; i++) {
    files.push({
      fileName: `duplicate-${i}.jpg`,
      filePath: `/test/photos/path${i}/duplicate-${i}.jpg`,
      size: fileSize,
      hash: hash,
      createdAt: new Date(Date.now() - i * 86400000).toISOString(), // Each file 1 day older
    });
  }

  return {
    hash,
    files,
    totalSize: fileSize * fileCount,
  };
}

/**
 * Format bytes to human-readable size
 */
export function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

/**
 * Generate a random hash string
 */
export function generateHash(): string {
  return Array.from({ length: 64 }, () =>
    Math.floor(Math.random() * 16).toString(16)
  ).join('');
}

/**
 * Wait for a specified amount of time
 */
export function wait(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Clean up test data patterns
 */
export const TEST_DATA_PATTERNS = {
  directories: /^\/test\//,
  files: /test-file-\d+/,
  hashes: /^(hash-|duplicate-)/,
};
