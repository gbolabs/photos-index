import { Pipe, PipeTransform } from '@angular/core';

/**
 * Lookup table for camera models to commercial names.
 * Maps EXIF camera make/model to user-friendly names.
 */
const CAMERA_NAME_MAP: Record<string, string> = {
  // Apple iPhones
  'iPhone 1,1': 'iPhone (Original)',
  'iPhone 1,2': 'iPhone 3G',
  'iPhone 2,1': 'iPhone 3GS',
  'iPhone 3,1': 'iPhone 4',
  'iPhone 3,2': 'iPhone 4',
  'iPhone 3,3': 'iPhone 4 (CDMA)',
  'iPhone 4,1': 'iPhone 4S',
  'iPhone 5,1': 'iPhone 5',
  'iPhone 5,2': 'iPhone 5',
  'iPhone 5,3': 'iPhone 5c',
  'iPhone 5,4': 'iPhone 5c',
  'iPhone 6,1': 'iPhone 5s',
  'iPhone 6,2': 'iPhone 5s',
  'iPhone 7,1': 'iPhone 6 Plus',
  'iPhone 7,2': 'iPhone 6',
  'iPhone 8,1': 'iPhone 6s',
  'iPhone 8,2': 'iPhone 6s Plus',
  'iPhone 8,4': 'iPhone SE',
  'iPhone 9,1': 'iPhone 7',
  'iPhone 9,2': 'iPhone 7 Plus',
  'iPhone 9,3': 'iPhone 7',
  'iPhone 9,4': 'iPhone 7 Plus',
  'iPhone 10,1': 'iPhone 8',
  'iPhone 10,2': 'iPhone 8 Plus',
  'iPhone 10,3': 'iPhone X',
  'iPhone 10,4': 'iPhone 8',
  'iPhone 10,5': 'iPhone 8 Plus',
  'iPhone 10,6': 'iPhone X',
  'iPhone 11,2': 'iPhone XS',
  'iPhone 11,4': 'iPhone XS Max',
  'iPhone 11,6': 'iPhone XS Max',
  'iPhone 11,8': 'iPhone XR',
  'iPhone 12,1': 'iPhone 11',
  'iPhone 12,3': 'iPhone 11 Pro',
  'iPhone 12,5': 'iPhone 11 Pro Max',
  'iPhone 12,8': 'iPhone SE (2nd gen)',
  'iPhone 13,1': 'iPhone 12 mini',
  'iPhone 13,2': 'iPhone 12',
  'iPhone 13,3': 'iPhone 12 Pro',
  'iPhone 13,4': 'iPhone 12 Pro Max',
  'iPhone 14,2': 'iPhone 13 Pro',
  'iPhone 14,3': 'iPhone 13 Pro Max',
  'iPhone 14,4': 'iPhone 13 mini',
  'iPhone 14,5': 'iPhone 13',
  'iPhone 14,6': 'iPhone SE (3rd gen)',
  'iPhone 14,7': 'iPhone 14',
  'iPhone 14,8': 'iPhone 14 Plus',
  'iPhone 15,2': 'iPhone 14 Pro',
  'iPhone 15,3': 'iPhone 14 Pro Max',
  'iPhone 15,4': 'iPhone 15',
  'iPhone 15,5': 'iPhone 15 Plus',
  'iPhone 16,1': 'iPhone 15 Pro',
  'iPhone 16,2': 'iPhone 15 Pro Max',
  'iPhone 17,1': 'iPhone 16 Pro',
  'iPhone 17,2': 'iPhone 16 Pro Max',
  'iPhone 17,3': 'iPhone 16',
  'iPhone 17,4': 'iPhone 16 Plus',

  // Samsung Galaxy
  'SM-G950F': 'Galaxy S8',
  'SM-G950U': 'Galaxy S8',
  'SM-G955F': 'Galaxy S8+',
  'SM-G955U': 'Galaxy S8+',
  'SM-G960F': 'Galaxy S9',
  'SM-G960U': 'Galaxy S9',
  'SM-G965F': 'Galaxy S9+',
  'SM-G965U': 'Galaxy S9+',
  'SM-G970F': 'Galaxy S10e',
  'SM-G973F': 'Galaxy S10',
  'SM-G975F': 'Galaxy S10+',
  'SM-G980F': 'Galaxy S20',
  'SM-G981U': 'Galaxy S20 5G',
  'SM-G985F': 'Galaxy S20+',
  'SM-G988B': 'Galaxy S20 Ultra',
  'SM-G991B': 'Galaxy S21',
  'SM-G996B': 'Galaxy S21+',
  'SM-G998B': 'Galaxy S21 Ultra',
  'SM-S901B': 'Galaxy S22',
  'SM-S906B': 'Galaxy S22+',
  'SM-S908B': 'Galaxy S22 Ultra',
  'SM-S911B': 'Galaxy S23',
  'SM-S916B': 'Galaxy S23+',
  'SM-S918B': 'Galaxy S23 Ultra',
  'SM-S921B': 'Galaxy S24',
  'SM-S926B': 'Galaxy S24+',
  'SM-S928B': 'Galaxy S24 Ultra',

  // Google Pixel
  'Pixel': 'Pixel',
  'Pixel XL': 'Pixel XL',
  'Pixel 2': 'Pixel 2',
  'Pixel 2 XL': 'Pixel 2 XL',
  'Pixel 3': 'Pixel 3',
  'Pixel 3 XL': 'Pixel 3 XL',
  'Pixel 3a': 'Pixel 3a',
  'Pixel 3a XL': 'Pixel 3a XL',
  'Pixel 4': 'Pixel 4',
  'Pixel 4 XL': 'Pixel 4 XL',
  'Pixel 4a': 'Pixel 4a',
  'Pixel 4a (5G)': 'Pixel 4a 5G',
  'Pixel 5': 'Pixel 5',
  'Pixel 5a': 'Pixel 5a',
  'Pixel 6': 'Pixel 6',
  'Pixel 6 Pro': 'Pixel 6 Pro',
  'Pixel 6a': 'Pixel 6a',
  'Pixel 7': 'Pixel 7',
  'Pixel 7 Pro': 'Pixel 7 Pro',
  'Pixel 7a': 'Pixel 7a',
  'Pixel 8': 'Pixel 8',
  'Pixel 8 Pro': 'Pixel 8 Pro',
  'Pixel 8a': 'Pixel 8a',
  'Pixel 9': 'Pixel 9',
  'Pixel 9 Pro': 'Pixel 9 Pro',
  'Pixel 9 Pro XL': 'Pixel 9 Pro XL',

  // Canon
  'Canon EOS 5D Mark II': 'Canon EOS 5D Mark II',
  'Canon EOS 5D Mark III': 'Canon EOS 5D Mark III',
  'Canon EOS 5D Mark IV': 'Canon EOS 5D Mark IV',
  'Canon EOS 6D': 'Canon EOS 6D',
  'Canon EOS 6D Mark II': 'Canon EOS 6D Mark II',
  'Canon EOS 7D': 'Canon EOS 7D',
  'Canon EOS 7D Mark II': 'Canon EOS 7D Mark II',
  'Canon EOS 80D': 'Canon EOS 80D',
  'Canon EOS 90D': 'Canon EOS 90D',
  'Canon EOS R': 'Canon EOS R',
  'Canon EOS R5': 'Canon EOS R5',
  'Canon EOS R6': 'Canon EOS R6',
  'Canon EOS R7': 'Canon EOS R7',
  'Canon EOS R8': 'Canon EOS R8',

  // Nikon
  'NIKON D850': 'Nikon D850',
  'NIKON D810': 'Nikon D810',
  'NIKON D750': 'Nikon D750',
  'NIKON D7500': 'Nikon D7500',
  'NIKON D5600': 'Nikon D5600',
  'NIKON Z 6': 'Nikon Z6',
  'NIKON Z 6_2': 'Nikon Z6 II',
  'NIKON Z 7': 'Nikon Z7',
  'NIKON Z 7_2': 'Nikon Z7 II',
  'NIKON Z 8': 'Nikon Z8',
  'NIKON Z 9': 'Nikon Z9',
  'NIKON Z fc': 'Nikon Z fc',

  // Sony
  'ILCE-7M3': 'Sony A7 III',
  'ILCE-7M4': 'Sony A7 IV',
  'ILCE-7RM3': 'Sony A7R III',
  'ILCE-7RM4': 'Sony A7R IV',
  'ILCE-7RM5': 'Sony A7R V',
  'ILCE-7SM3': 'Sony A7S III',
  'ILCE-9M2': 'Sony A9 II',
  'ILCE-1': 'Sony A1',
  'ILCE-6400': 'Sony A6400',
  'ILCE-6600': 'Sony A6600',
  'ILCE-6700': 'Sony A6700',
  'ZV-E10': 'Sony ZV-E10',
  'ZV-1': 'Sony ZV-1',

  // Fujifilm
  'X-T4': 'Fujifilm X-T4',
  'X-T5': 'Fujifilm X-T5',
  'X-T30': 'Fujifilm X-T30',
  'X-T30 II': 'Fujifilm X-T30 II',
  'X-S10': 'Fujifilm X-S10',
  'X-S20': 'Fujifilm X-S20',
  'X100V': 'Fujifilm X100V',
  'X100VI': 'Fujifilm X100VI',
  'GFX 50S II': 'Fujifilm GFX 50S II',
  'GFX 100S': 'Fujifilm GFX 100S',

  // DJI Drones
  'FC220': 'DJI Mavic Pro',
  'FC330': 'DJI Phantom 4',
  'FC6310': 'DJI Phantom 4 Pro',
  'FC7303': 'DJI Mini 2',
  'L1D-20c': 'DJI Mavic 2 Pro',
  'FC3170': 'DJI Mavic Air 2',
  'FC3411': 'DJI Mini 3 Pro',
  'FC8282': 'DJI Mini 4 Pro',
};

@Pipe({
  name: 'cameraName',
  standalone: true,
})
export class CameraNamePipe implements PipeTransform {
  /**
   * Transform camera make and model into a user-friendly name.
   * @param model The camera model from EXIF
   * @param make Optional camera make from EXIF
   * @returns User-friendly camera name or original model if no mapping found
   */
  transform(model: string | null | undefined, make?: string | null): string {
    if (!model) return '-';

    // Try direct lookup first
    if (CAMERA_NAME_MAP[model]) {
      return CAMERA_NAME_MAP[model];
    }

    // Try with make prefix for Apple devices
    if (make?.toLowerCase().includes('apple')) {
      const appleKey = model.replace('Apple ', '');
      if (CAMERA_NAME_MAP[appleKey]) {
        return CAMERA_NAME_MAP[appleKey];
      }
    }

    // Try combining make and model for other devices
    if (make) {
      const combined = `${make} ${model}`;
      if (CAMERA_NAME_MAP[combined]) {
        return CAMERA_NAME_MAP[combined];
      }
    }

    // Return original model if no mapping found
    return model;
  }
}
