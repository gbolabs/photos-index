import { FileSizePipe } from './file-size.pipe';

describe('FileSizePipe', () => {
  let pipe: FileSizePipe;

  beforeEach(() => {
    pipe = new FileSizePipe();
  });

  it('should create an instance', () => {
    expect(pipe).toBeTruthy();
  });

  it('should format 0 bytes', () => {
    expect(pipe.transform(0)).toBe('0 Bytes');
  });

  it('should format kilobytes', () => {
    expect(pipe.transform(1024)).toBe('1 KB');
  });

  it('should format megabytes', () => {
    expect(pipe.transform(1048576)).toBe('1 MB');
  });
});
